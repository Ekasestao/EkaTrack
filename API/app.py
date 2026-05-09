from flask import Flask, request, jsonify, session
from flask_cors import CORS
from werkzeug.security import generate_password_hash, check_password_hash
from secretKey import secret_key, api_key, api_url, database
import sqlite3
import requests

app = Flask(__name__)
app.config["SECRET_KEY"] = secret_key
CORS(app, supports_credentials=True)

TMDB_API_KEY = api_key
TMDB_BASE_URL = api_url
DATABASE = database


# ─── Helpers ────────────────────────────────────────────────────────────────


def get_db():
    conn = sqlite3.connect(DATABASE)
    conn.row_factory = sqlite3.Row
    return conn


def init_db():
    conn = get_db()
    conn.executescript("""
        CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            username TEXT NOT NULL UNIQUE,
            email TEXT NOT NULL UNIQUE,
            password TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS watchlist (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id INTEGER NOT NULL,
            tmdb_id INTEGER NOT NULL,
            media_type TEXT NOT NULL CHECK(media_type IN ('movie', 'tv')),
            title TEXT NOT NULL,
            poster_path TEXT,
            status TEXT NOT NULL DEFAULT 'pending' CHECK(status IN ('pending', 'watched')),
            rating INTEGER CHECK(rating BETWEEN 1 AND 10),
            notes TEXT,
            added_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            watched_at TIMESTAMP,
            FOREIGN KEY (user_id) REFERENCES users(id),
            UNIQUE(user_id, tmdb_id, media_type)
        );
    """)
    conn.commit()
    conn.close()


# ─── Health Check ────────────────────────────────────────────────────────────────────


@app.route("/")
def health():
    return jsonify({"status": 200, "message": "Healthy"})


# ─── Auth ────────────────────────────────────────────────────────────────────


@app.route("/register", methods=["POST"])
def register():
    data = request.json
    username = data.get("username", "").strip()
    email = data.get("email", "").strip()
    password = data.get("password", "")
    same_password = data.get("same_password", "")

    if not username:
        return jsonify({"status": 400, "message": "Username requerido"})
    if not email:
        return jsonify({"status": 400, "message": "Email requerido"})
    if not password:
        return jsonify({"status": 400, "message": "Password requerido"})
    if password != same_password:
        return jsonify({"status": 400, "message": "Las contraseñas no coinciden"})

    hashed_pw = generate_password_hash(password)

    try:
        conn = get_db()
        conn.execute(
            "INSERT INTO users (username, email, password) VALUES (?, ?, ?)",
            (username, email, hashed_pw),
        )
        conn.commit()
        user = conn.execute(
            "SELECT * FROM users WHERE username = ?", (username,)
        ).fetchone()
        session["user_id"] = user["id"]
        session["username"] = user["username"]
        conn.close()
        return jsonify(
            {
                "status": 200,
                "user": {"id": user["id"], "username": username, "email": email},
            }
        )
    except sqlite3.IntegrityError:
        return jsonify({"status": 409, "message": "Username o email ya en uso"})


@app.route("/login", methods=["POST"])
def login():
    data = request.json
    credential = data.get("login_credential", "").strip()
    password = data.get("password", "")

    conn = get_db()
    user = conn.execute(
        "SELECT * FROM users WHERE username = ? OR email = ?", (credential, credential)
    ).fetchone()
    conn.close()

    if not user:
        return jsonify({"status": 404, "message": "Usuario no encontrado"})
    if not check_password_hash(user["password"], password):
        return jsonify({"status": 401, "message": "Contraseña incorrecta"})

    session["user_id"] = user["id"]
    session["username"] = user["username"]
    return jsonify(
        {
            "status": 200,
            "user": {
                "id": user["id"],
                "username": user["username"],
                "email": user["email"],
            },
        }
    )


@app.route("/logout", methods=["POST"])
def logout():
    session.clear()
    return jsonify({"status": 200})


@app.route("/me")
def me():
    if "user_id" not in session:
        return jsonify({"status": 401, "message": "No autenticado"})
    return jsonify(
        {
            "status": 200,
            "user": {"id": session["user_id"], "username": session["username"]},
        }
    )


# ─── TMDB proxy ──────────────────────────────────────────────────────────────


@app.route("/search")
def search():
    query = request.args.get("q", "")
    page = request.args.get("page", 1)
    if not query:
        return jsonify({"results": []})

    response = requests.get(
        f"{TMDB_BASE_URL}/search/multi",
        params={
            "api_key": TMDB_API_KEY,
            "query": query,
            "page": page,
            "language": "es-ES",
        },
    )
    return jsonify(response.json())


@app.route("/media/<string:media_type>/<int:tmdb_id>")
def media_detail(media_type, tmdb_id):
    if media_type not in ("movie", "tv"):
        return jsonify({"status": 400, "message": "Tipo inválido"})

    response = requests.get(
        f"{TMDB_BASE_URL}/{media_type}/{tmdb_id}",
        params={
            "api_key": TMDB_API_KEY,
            "language": "es-ES",
            "append_to_response": "credits",
        },
    )
    return jsonify(response.json())


# ─── Watchlist ───────────────────────────────────────────────────────────────


@app.route("/watchlist", methods=["GET", "POST"])
def watchlist():
    if "user_id" not in session:
        return jsonify({"status": 401, "message": "No autenticado"})

    user_id = session["user_id"]
    conn = get_db()

    if request.method == "GET":
        status_filter = request.args.get("status")
        query = "SELECT * FROM watchlist WHERE user_id = ?"
        params = [user_id]
        if status_filter in ("pending", "watched"):
            query += " AND status = ?"
            params.append(status_filter)
        query += " ORDER BY added_at DESC"
        items = conn.execute(query, params).fetchall()
        conn.close()
        return jsonify([dict(i) for i in items])

    if request.method == "POST":
        data = request.json
        try:
            conn.execute(
                """
                INSERT INTO watchlist (user_id, tmdb_id, media_type, title, poster_path)
                VALUES (?, ?, ?, ?, ?)
            """,
                (
                    user_id,
                    data["tmdb_id"],
                    data["media_type"],
                    data["title"],
                    data.get("poster_path"),
                ),
            )
            conn.commit()
            conn.close()
            return jsonify({"status": 200, "message": "Añadido a la watchlist"})
        except sqlite3.IntegrityError:
            conn.close()
            return jsonify({"status": 409, "message": "Ya está en tu watchlist"})


@app.route("/watchlist/<int:item_id>", methods=["PATCH", "DELETE"])
def watchlist_item(item_id):
    if "user_id" not in session:
        return jsonify({"status": 401, "message": "No autenticado"})

    user_id = session["user_id"]
    conn = get_db()
    item = conn.execute(
        "SELECT * FROM watchlist WHERE id = ? AND user_id = ?", (item_id, user_id)
    ).fetchone()

    if not item:
        conn.close()
        return jsonify({"status": 404, "message": "Item no encontrado"})

    if request.method == "PATCH":
        data = request.json
        fields = []
        params = []
        if "status" in data:
            fields.append("status = ?")
            params.append(data["status"])
            if data["status"] == "watched":
                fields.append("watched_at = CURRENT_TIMESTAMP")
        if "rating" in data:
            fields.append("rating = ?")
            params.append(data["rating"])
        if "notes" in data:
            fields.append("notes = ?")
            params.append(data["notes"])

        if fields:
            params.append(item_id)
            conn.execute(
                f"UPDATE watchlist SET {', '.join(fields)} WHERE id = ?", params
            )
            conn.commit()

        conn.close()
        return jsonify({"status": 200})

    if request.method == "DELETE":
        conn.execute("DELETE FROM watchlist WHERE id = ?", (item_id,))
        conn.commit()
        conn.close()
        return jsonify({"status": 200})


# ─── Init ────────────────────────────────────────────────────────────────────

init_db()

if __name__ == "__main__":
    app.run(debug=True)
