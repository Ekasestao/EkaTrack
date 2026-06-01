from flask import Flask, request, jsonify, session
from flask_cors import CORS
from werkzeug.security import generate_password_hash, check_password_hash
from secretKey import secret_key, api_key, api_url, api_url_tvmaze, database
from itsdangerous import URLSafeTimedSerializer
import sqlite3
import requests
import json
import time
from datetime import datetime
import re

token_serializer = URLSafeTimedSerializer(secret_key, salt="auth")
TOKEN_MAX_AGE = 2592000  # 30 días

app = Flask(__name__)
app.config["SECRET_KEY"] = secret_key
app.config["SESSION_COOKIE_SAMESITE"] = "None"
app.config["SESSION_COOKIE_SECURE"] = True
app.config["SESSION_COOKIE_HTTPONLY"] = True
app.config["SESSION_COOKIE_PERMANENT"] = True
app.config["PERMANENT_SESSION_LIFETIME"] = 2592000
CORS(
    app,
    supports_credentials=True,
    origins=[
        "https://localhost:7094",
        "http://localhost:7094",
        "https://ekasestao.github.io",
    ],
    allow_headers=["Content-Type", "Authorization"],
)


@app.after_request
def add_security_headers(response):
    response.headers["Content-Security-Policy"] = (
        "default-src 'none'; frame-ancestors 'none'"
    )
    response.headers["Strict-Transport-Security"] = (
        "max-age=31536000; includeSubDomains"
    )
    response.headers["X-Content-Type-Options"] = "nosniff"
    response.headers["X-Frame-Options"] = "DENY"
    response.headers["Referrer-Policy"] = "strict-origin-when-cross-origin"
    return response

TMDB_API_KEY = api_key
TMDB_BASE_URL = api_url
TVMAZE_BASE_URL = api_url_tvmaze
DATABASE = database

# ─── TMDB Cache (5 min TTL) ────────────────────────────────────────────────
_tmdb_cache = {}


def tmdb_get(url, params=None, timeout=15):
    cache_key = f"{url}|{json.dumps(params, sort_keys=True) if params else ''}"
    now = time.time()
    cached = _tmdb_cache.get(cache_key)
    if cached and now - cached["time"] < 300:
        return cached["response"]
    resp = requests.get(url, params=params, timeout=timeout)
    _tmdb_cache[cache_key] = {"response": resp, "time": now}
    return resp


def tmdb_get_json(url, params=None, timeout=15):
    r = tmdb_get(url, params, timeout)
    r.raise_for_status()
    return r.json()


# ─── Helpers ────────────────────────────────────────────────────────────────


def get_db():
    conn = sqlite3.connect(DATABASE)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    return conn


def init_db():
    conn = get_db()
    conn.executescript("""
        CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            username TEXT NOT NULL UNIQUE,
            email TEXT NOT NULL UNIQUE,
            password TEXT NOT NULL,
            tmdb_guest_session_id TEXT
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

        CREATE TABLE IF NOT EXISTS lists (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id INTEGER NOT NULL,
            name TEXT NOT NULL,
            description TEXT DEFAULT '',
            list_type TEXT NOT NULL DEFAULT 'custom'
                CHECK(list_type IN ('watchlist', 'watched', 'liked', 'custom')),
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (user_id) REFERENCES users(id)
        );

        CREATE TABLE IF NOT EXISTS list_items (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            list_id INTEGER NOT NULL,
            tmdb_id INTEGER NOT NULL,
            media_type TEXT NOT NULL CHECK(media_type IN ('movie', 'tv')),
            title TEXT NOT NULL,
            poster_path TEXT,
            added_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (list_id) REFERENCES lists(id) ON DELETE CASCADE,
            UNIQUE(list_id, tmdb_id, media_type)
        );
        CREATE TABLE IF NOT EXISTS episode_tracking (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id INTEGER NOT NULL,
            tvmaze_episode_id INTEGER NOT NULL,
            season_number INTEGER NOT NULL,
            episode_number INTEGER NOT NULL,
            show_title TEXT NOT NULL DEFAULT '',
            watched INTEGER DEFAULT 0,
            watched_at TIMESTAMP,
            FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
            UNIQUE(user_id, tvmaze_episode_id)
        );
    """)
    conn.commit()

    # Migration: add tvmaze_show_id to list_items if not present
    try:
        conn.execute("ALTER TABLE list_items ADD COLUMN tvmaze_show_id INTEGER")
        conn.commit()
    except sqlite3.OperationalError:
        pass

    # Migration: add vote_average to list_items if not present
    try:
        conn.execute("ALTER TABLE list_items ADD COLUMN vote_average REAL")
        conn.commit()
    except sqlite3.OperationalError:
        pass

    # Backfill vote_average for existing items via TMDB
    missing = conn.execute(
        "SELECT id, tmdb_id, media_type, title FROM list_items WHERE vote_average IS NULL OR vote_average <= 0"
    ).fetchall()
    if missing:
        for row in missing:
            try:
                url = f"{TMDB_BASE_URL}/{row['media_type']}/{row['tmdb_id']}"
                data = tmdb_get_json(url, {"api_key": TMDB_API_KEY})
                va = data.get("vote_average")
                if va and va > 0:
                    conn.execute(
                        "UPDATE list_items SET vote_average = ? WHERE id = ?",
                        (va, row["id"]),
                    )
                    conn.commit()
                elif row["media_type"] == "tv":
                    tvmaze_url = f"{TVMAZE_BASE_URL}/singlesearch/shows"
                    tvmaze_resp = requests.get(
                        tvmaze_url,
                        params={"q": row["title"]},
                        timeout=10,
                    )
                    if tvmaze_resp.ok:
                        tvmaze_show = tvmaze_resp.json()
                        tvmaze_rating = tvmaze_show.get("rating", {}).get("average")
                        if tvmaze_rating and tvmaze_rating > 0:
                            conn.execute(
                                "UPDATE list_items SET vote_average = ? WHERE id = ?",
                                (tvmaze_rating, row["id"]),
                            )
                            conn.commit()
                time.sleep(0.05)
            except Exception:
                continue

    conn.close()


def ensure_default_lists(user_id):
    conn = get_db()
    existing = conn.execute(
        "SELECT list_type FROM lists WHERE user_id = ?", (user_id,)
    ).fetchall()
    existing_types = {r["list_type"] for r in existing}

    defaults = [
        ("watchlist", "Watchlist", ""),
        ("watched", "Visto", ""),
        ("liked", "Me gusta", ""),
    ]
    for list_type, name, desc in defaults:
        if list_type not in existing_types:
            conn.execute(
                "INSERT INTO lists (user_id, name, description, list_type) VALUES (?, ?, ?, ?)",
                (user_id, name, desc, list_type),
            )
    conn.commit()
    conn.close()


# ─── Health Check ───────────────────────────────────────────────────────────


@app.route("/")
def health():
    return jsonify({"status": 200, "message": "Healthy"})


# ─── Brute-force protection ─────────────────────────────────────────────────

import threading
from collections import defaultdict

_login_attempts: dict[str, list[float]] = defaultdict(list)
_lockout_durations = [60, 120, 300, 600, 1800, 3600]
_attempt_limit = 5
_cleanup_interval = 3600
_last_cleanup = time.time()
_lock = threading.Lock()


def _cleanup_old_attempts():
    global _last_cleanup
    now = time.time()
    if now - _last_cleanup < _cleanup_interval:
        return
    cutoff = now - max(_lockout_durations)
    for ip in list(_login_attempts.keys()):
        _login_attempts[ip] = [t for t in _login_attempts[ip] if t > cutoff]
        if not _login_attempts[ip]:
            del _login_attempts[ip]
    _last_cleanup = now


def _get_client_ip() -> str:
    return request.headers.get("X-Forwarded-For", request.remote_addr or "unknown")


def _get_lockout_seconds(ip: str) -> int:
    with _lock:
        attempts = _login_attempts.get(ip, [])
        if len(attempts) < _attempt_limit:
            return 0
        recent = [t for t in attempts if t > time.time() - max(_lockout_durations)]
        if len(recent) < _attempt_limit:
            return 0
        offence = (len(recent) - _attempt_limit) // _attempt_limit
        duration = _lockout_durations[min(offence, len(_lockout_durations) - 1)]
        elapsed = time.time() - recent[0]
        remaining = int(duration - elapsed)
        return max(remaining, 0)


def _record_attempt(ip: str, success: bool):
    now = time.time()
    with _lock:
        if success:
            _login_attempts.pop(ip, None)
        else:
            _login_attempts[ip].append(now)
            _cleanup_old_attempts()


# ─── Auth ──────────────────────────────────────────────────────────────────


@app.route("/register", methods=["POST"])
def register():
    return jsonify({"status": 403, "message": "Registro cerrado"}), 403


@app.route("/login", methods=["POST"])
def login():
    ip = _get_client_ip()
    remaining = _get_lockout_seconds(ip)
    if remaining > 0:
        minutes = remaining // 60
        seconds = remaining % 60
        if minutes > 0:
            msg = f"Demasiados intentos. Intenta de nuevo en {minutes} min {seconds} s."
        else:
            msg = f"Demasiados intentos. Intenta de nuevo en {seconds} s."
        return jsonify({"status": 429, "message": msg}), 429

    data = request.json
    credential = (data.get("login_credential") or "").strip()
    password = data.get("password") or ""

    if not credential or not password:
        _record_attempt(ip, False)
        return jsonify({"status": 400, "message": "Credencial y contraseña requeridos"}), 400

    conn = get_db()
    user = conn.execute(
        "SELECT * FROM users WHERE username = ? OR email = ?",
        (credential, credential),
    ).fetchone()
    conn.close()

    if not user:
        _record_attempt(ip, False)
        return jsonify({"status": 404, "message": "Usuario no encontrado"}), 404
    if not check_password_hash(user["password"], password):
        _record_attempt(ip, False)
        return jsonify({"status": 401, "message": "Contraseña incorrecta"}), 401

    _record_attempt(ip, True)
    session.permanent = True
    session["user_id"] = user["id"]
    session["username"] = user["username"]

    ensure_default_lists(user["id"])

    token = token_serializer.dumps({"user_id": user["id"], "username": user["username"]})

    return jsonify(
        {
            "status": 200,
            "user": {
                "id": user["id"],
                "username": user["username"],
                "email": user["email"],
            },
            "token": token,
        }
    )


@app.route("/logout", methods=["POST"])
def logout():
    session.clear()
    return jsonify({"status": 200})


@app.route("/me")
def me():
    user_id = get_user_id()
    if user_id is None:
        return jsonify({"status": 401, "message": "No autenticado"}), 401

    ensure_default_lists(user_id)

    username = session.get("username")
    if username is None:
        conn = get_db()
        row = conn.execute("SELECT username FROM users WHERE id = ?", (user_id,)).fetchone()
        conn.close()
        username = row["username"] if row else None

    token = token_serializer.dumps({"user_id": user_id, "username": username})

    return jsonify(
        {
            "status": 200,
            "user": {"id": user_id, "username": username},
            "token": token,
        }
    )


# ─── TMDB proxy ──────────────────────────────────────────────────────────────


@app.route("/trending")
def trending():
    media_type = request.args.get("media_type", "all")
    time_window = request.args.get("time_window", "week")
    page = request.args.get("page", 1)
    data = tmdb_get_json(
        f"{TMDB_BASE_URL}/trending/{media_type}/{time_window}",
        {"api_key": TMDB_API_KEY, "language": "es-ES", "page": page},
    )
    return jsonify(data)


@app.route("/search")
def search():
    query = request.args.get("q", "")
    page = request.args.get("page", 1)
    if not query:
        return jsonify({"results": []})

    data = tmdb_get_json(
        f"{TMDB_BASE_URL}/search/multi",
        {
            "api_key": TMDB_API_KEY,
            "query": query,
            "page": page,
            "language": "es-ES",
        },
    )
    return jsonify(data)


@app.route("/nuevo/estrenos")
def nuevo_estrenos():
    page = request.args.get("page", 1)
    data = tmdb_get_json(
        f"{TMDB_BASE_URL}/movie/now_playing",
        {
            "api_key": TMDB_API_KEY,
            "language": "es-ES",
            "region": "ES",
            "page": page,
        },
    )
    for item in data.get("results", []):
        item["media_type"] = "movie"
    return jsonify(data)


@app.route("/nuevo/proximamente")
def nuevo_proximamente():
    page = request.args.get("page", 1)
    data = tmdb_get_json(
        f"{TMDB_BASE_URL}/movie/upcoming",
        {
            "api_key": TMDB_API_KEY,
            "language": "es-ES",
            "region": "ES",
            "page": page,
        },
    )
    today = datetime.now().strftime("%Y-%m-%d")
    results = []
    for item in data.get("results", []):
        item["media_type"] = "movie"
        rd = item.get("release_date")
        if rd and rd >= today:
            results.append(item)
    data["results"] = results
    return jsonify(data)


@app.route("/media/<string:media_type>/<int:tmdb_id>")
def media_detail(media_type, tmdb_id):
    if media_type not in ("movie", "tv"):
        return jsonify({"status": 400, "message": "Tipo inválido"}), 400

    append = "credits,watch/providers,recommendations"
    if media_type == "movie":
        append += ",release_dates"
    else:
        append += ",content_ratings"

    data = tmdb_get_json(
        f"{TMDB_BASE_URL}/{media_type}/{tmdb_id}",
        {
            "api_key": TMDB_API_KEY,
            "language": "es-ES",
            "append_to_response": append,
        },
    )

    # ─── TMDB duplicate fallback: TVmaze chain ───────────────────────────
    def _tvm_status(s):
        return {
            "Running": "Returning Series",
            "Ended": "Ended",
            "To Be Determined": "Planned",
            "In Development": "In Production",
        }.get(s, s)

    def _is_incomplete(d):
        if not d.get("overview"):
            return True
        if not d.get("poster_path"):
            return True
        if not d.get("vote_average"):
            return True
        if media_type == "tv":
            real = [s for s in d.get("seasons", []) if s.get("season_number", 0) > 0]
            if not real:
                return True
        return False

    if _is_incomplete(data):
        search_terms = list(
            dict.fromkeys(
                filter(
                    None,
                    [
                        data.get("original_title"),
                        data.get("original_name"),
                        data.get("title"),
                        data.get("name"),
                    ],
                )
            )
        )

        # Add TMDB alternative titles (often Latin script for non-Latin entries)
        try:
            alt_data = tmdb_get_json(
                f"{TMDB_BASE_URL}/{media_type}/{tmdb_id}/alternative_titles",
                {"api_key": TMDB_API_KEY},
            )
            for r in alt_data.get("results", []):
                t = r.get("title")
                if t and t not in search_terms:
                    search_terms.append(t)
        except requests.RequestException:
            pass

        # Search TVmaze by each term
        tvmaze_show = None
        tvmaze_headers = {"Accept-Language": "es"}
        for term in search_terms:
            if tvmaze_show:
                break
            try:
                r = requests.get(
                    f"{TVMAZE_BASE_URL}/singlesearch/shows",
                    params={"q": term},
                    headers=tvmaze_headers,
                    timeout=10,
                )
                if r.ok:
                    tvmaze_show = r.json()
            except requests.RequestException:
                continue

        if tvmaze_show:
            overview = re.sub(r"<[^>]+>", "", tvmaze_show.get("summary") or "").strip()
            if overview:
                data["overview"] = overview
            if tvmaze_show.get("name"):
                data["name"] = tvmaze_show["name"]
            rating = tvmaze_show.get("rating", {}).get("average")
            if rating is not None:
                data["vote_average"] = rating
            if tvmaze_show.get("status"):
                data["status"] = _tvm_status(tvmaze_show["status"])
            if tvmaze_show.get("genres"):
                data["genres"] = [{"id": 0, "name": g} for g in tvmaze_show["genres"]]
            if tvmaze_show.get("premiered"):
                data["first_air_date"] = tvmaze_show["premiered"]
            runtime = tvmaze_show.get("averageRuntime") or tvmaze_show.get("runtime")
            if runtime:
                data["runtime"] = runtime
            network = tvmaze_show.get("network")
            if network and network.get("name"):
                data["networks"] = [{"id": 0, "name": network["name"]}]
                cc = network.get("country", {}).get("code")
                if cc:
                    data["origin_country"] = [cc]
                    data["production_countries"] = [
                        {"iso_3166_1": cc, "name": network["country"].get("name", "")}
                    ]

    VALID_ES_CERTS = {"APTA", "7", "12", "16", "18", "X"}

    certification = None
    if media_type == "movie":
        # Prefer theatrical (type 3) → limited (2) → digital (4) → physical (5) → TV (6) → premiere (1)
        type_priority = {3: 0, 2: 1, 4: 2, 5: 3, 6: 4, 1: 5}
        es_dates = []
        for country in data.get("release_dates", {}).get("results", []):
            if country.get("iso_3166_1") == "ES":
                es_dates = country.get("release_dates", [])
                break
        best = None
        best_rank = 99
        for rd in es_dates:
            cert = rd.get("certification", "").strip()
            if cert in VALID_ES_CERTS:
                rank = type_priority.get(rd.get("type"), 99)
                if rank < best_rank:
                    best_rank = rank
                    best = cert
        certification = best or "APTA"
        data.pop("release_dates", None)
    else:
        for country in data.get("content_ratings", {}).get("results", []):
            if country.get("iso_3166_1") == "ES":
                cert = country.get("rating")
                if cert in VALID_ES_CERTS:
                    certification = cert
                break
        data.pop("content_ratings", None)

    data["certification"] = certification

    # ─── Videos (trailer) — llamada aparte sin idioma para obtener más resultados ──
    trailer = None
    try:
        vid_data = tmdb_get_json(
            f"{TMDB_BASE_URL}/{media_type}/{tmdb_id}/videos",
            {"api_key": TMDB_API_KEY},
        )
        for v in vid_data.get("results", []):
            if v.get("type") == "Trailer" and v.get("site") == "YouTube":
                trailer = {"key": v["key"], "name": v.get("name")}
                break
    except requests.RequestException:
        pass
    data["trailer"] = trailer

    # ─── Watch providers (ES) ───────────────────────────────────────────────
    providers = None
    wp = data.get("watch/providers", {}).get("results", {})
    es_providers = wp.get("ES")
    if es_providers:
        providers = {}
        for kind in ("flatrate", "rent", "buy"):
            items = es_providers.get(kind)
            if items:
                providers[kind] = [
                    {
                        "id": p["provider_id"],
                        "name": p["provider_name"],
                        "logo": p.get("logo_path"),
                    }
                    for p in items
                ]
    data.pop("watch/providers", None)
    data["providers"] = providers

    # ─── Recommendations ────────────────────────────────────────────────────
    recs = []
    for r in data.get("recommendations", {}).get("results", [])[:12]:
        recs.append(
            {
                "id": r["id"],
                "title": r.get("title") or r.get("name"),
                "poster_path": r.get("poster_path"),
                "media_type": r.get("media_type", media_type),
                "vote_average": r.get("vote_average", 0),
            }
        )
    data.pop("recommendations", None)
    data["recommendations"] = recs

    # ─── TV: filtrar especiales ──────────────────────────────────────────
    if media_type == "tv":
        if "seasons" in data:
            data["seasons"] = [
                s for s in data["seasons"] if s.get("season_number", 0) > 0
            ]
            data["number_of_seasons"] = len(data["seasons"])

    return jsonify(data)


# ─── TVmaze ─────────────────────────────────────────────────────────────────


@app.route("/media/tv/<int:tmdb_id>/tvmaze")
def tvmaze_show(tmdb_id):
    user_id = get_user_id()

    # 1. Get IMDb ID from TMDB external_ids
    imdb_id = None
    try:
        ext_data = tmdb_get_json(
            f"{TMDB_BASE_URL}/tv/{tmdb_id}/external_ids",
            {"api_key": TMDB_API_KEY},
        )
        imdb_id = ext_data.get("imdb_id")
    except requests.RequestException:
        pass

    # 2. Lookup TVmaze show by IMDb ID, fallback by name
    tvmaze_show = None
    tvmaze_headers = {"Accept-Language": "es"}
    if imdb_id:
        try:
            lookup_resp = requests.get(
                f"{TVMAZE_BASE_URL}/lookup/shows",
                params={"imdb": imdb_id},
                headers=tvmaze_headers,
            )
            if lookup_resp.ok:
                tvmaze_show = lookup_resp.json()
        except requests.RequestException:
            pass

    if not tvmaze_show:
        try:
            detail_data = tmdb_get_json(
                f"{TMDB_BASE_URL}/tv/{tmdb_id}",
                {"api_key": TMDB_API_KEY, "language": "es-ES"},
            )
            name = detail_data.get("name", "")
            first_air_date = detail_data.get("first_air_date", "")
            year = (
                first_air_date[:4]
                if first_air_date and len(first_air_date) >= 4
                else None
            )
            if name:
                # Try searching with name fragments (e.g., after ":")
                search_names = [name]
                for sep in [":", " - ", " — ", " – ", "—", "–"]:
                    parts = name.split(sep)
                    if len(parts) > 1:
                        for part in parts:
                            stripped = part.strip()
                            if stripped and stripped not in search_names:
                                search_names.append(stripped)

                # Also try with year for specificity
                if year:
                    for sn in list(search_names):
                        with_year = f"{sn} {year}"
                        if with_year not in search_names:
                            search_names.append(with_year)

                for search_name in search_names:
                    try:
                        search_resp = requests.get(
                            f"{TVMAZE_BASE_URL}/singlesearch/shows",
                            params={"q": search_name},
                            headers=tvmaze_headers,
                            timeout=10,
                        )
                        if search_resp.ok:
                            tvmaze_show = search_resp.json()
                            break
                    except requests.RequestException:
                        continue

                # 3. Fallback: TVmaze /search/shows (plural, returns multiple results)
                if not tvmaze_show:
                    try:
                        search_resp = requests.get(
                            f"{TVMAZE_BASE_URL}/search/shows",
                            params={"q": name},
                            headers=tvmaze_headers,
                            timeout=10,
                        )
                        if search_resp.ok:
                            results = (
                                search_resp.json()
                            )  # list of {"score": ..., "show": {...}}
                            for result in results:
                                show = result.get("show", {})
                                if year:
                                    premiered = show.get(
                                        "premiered", ""
                                    )  # "YYYY-MM-DD"
                                    show_year = premiered[:4] if premiered else None
                                    if show_year and show_year != year:
                                        continue
                                tvmaze_show = show
                                break
                    except requests.RequestException:
                        pass
        except requests.RequestException:
            pass

    if not tvmaze_show:
        return _tvmaze_fallback(tmdb_id)

    tvmaze_id = tvmaze_show["id"]

    # 3. Fetch seasons from TVmaze
    try:
        seasons_resp = requests.get(
            f"{TVMAZE_BASE_URL}/shows/{tvmaze_id}/seasons", headers=tvmaze_headers
        )
        if not seasons_resp.ok:
            return jsonify({"tvmaze_id": tvmaze_id, "seasons": []})
        seasons_data = seasons_resp.json()
    except requests.RequestException:
        return jsonify({"tvmaze_id": tvmaze_id, "seasons": []})

    # 4. Get watched episodes for this user from local DB
    conn = get_db()
    watched_episodes = set()
    if user_id:
        rows = conn.execute(
            "SELECT tvmaze_episode_id FROM episode_tracking WHERE user_id = ? AND watched = 1",
            (user_id,),
        ).fetchall()
        watched_episodes = {r["tvmaze_episode_id"] for r in rows}
    conn.close()

    # 5. Fetch Spanish episode names + ratings from TMDB (by airdate + by season/ep)
    tmdb_by_date = {}  # airdate -> {ep_num: {"name": ..., "vote_average": ...}}
    tmdb_by_season = {}  # season_num -> {ep_num: ...}
    try:
        max_tmdb_seasons = (
            max((s.get("number", 0) for s in seasons_data), default=0) + 5
        )
        for s in range(1, max_tmdb_seasons + 1):
            season_data = tmdb_get_json(
                f"{TMDB_BASE_URL}/tv/{tmdb_id}/season/{s}",
                {"api_key": TMDB_API_KEY, "language": "es-ES"},
            )
            for ep in season_data.get("episodes", []):
                ep_num = ep.get("episode_number")
                if not ep_num:
                    continue
                data = {
                    "name": ep.get("name", "") or "",
                    "vote_average": ep.get("vote_average"),
                }
                tmdb_by_season.setdefault(s, {})[ep_num] = data
                airdate = ep.get("air_date")
                if airdate:
                    tmdb_by_date.setdefault(airdate, {})[ep_num] = data
    except Exception:
        pass

    def tmdb_ep_info(season_number, ep_num, airdate):
        """Lookup TMDB episode data — try season+ep first, fallback to airdate."""
        if season_number is not None and ep_num is not None:
            by_ep = tmdb_by_season.get(season_number)
            if by_ep is not None:
                if ep_num in by_ep:
                    return by_ep[ep_num]
                if len(by_ep) == 1:
                    return next(iter(by_ep.values()))
        if airdate:
            by_ep = tmdb_by_date.get(airdate)
            if by_ep is not None:
                if ep_num in by_ep:
                    return by_ep[ep_num]
                if len(by_ep) == 1:
                    return next(iter(by_ep.values()))
        return None

    # 6. Fetch episodes for each season from TVmaze
    result_seasons = []
    for season in seasons_data:
        season_id = season["id"]
        season_number = season.get("number", 0)
        if season_number == 0:
            continue  # skip specials

        episodes = []
        try:
            ep_resp = requests.get(
                f"{TVMAZE_BASE_URL}/seasons/{season_id}/episodes",
                headers=tvmaze_headers,
            )
            if ep_resp.ok:
                for ep in ep_resp.json():
                    ep_num = ep.get("number")
                    if ep_num is None:
                        continue  # skip specials/OVAs/un-aired dentro de la temporada
                    ep_season = ep.get("season", season_number)
                    tmdb_info = tmdb_ep_info(ep_season, ep_num, ep.get("airdate"))
                    episodes.append(
                        {
                            "tvmaze_episode_id": ep["id"],
                            "season_number": ep.get("season", season_number),
                            "episode_number": ep_num,
                            "name": (
                                tmdb_info.get("name")
                                if tmdb_info
                                else ep.get("name", "")
                            ),
                            "vote_average": (
                                tmdb_info.get("vote_average")
                                if tmdb_info
                                else (ep.get("rating") or {}).get("average")
                            ),
                            "still_url": (
                                ep.get("image", {}).get("medium")
                                if ep.get("image")
                                else None
                            ),
                            "air_date": ep.get("airdate", ""),
                            "runtime": ep.get("runtime"),
                            "summary": ep.get("summary", ""),
                            "watched": ep["id"] in watched_episodes,
                        }
                    )
        except Exception:
            pass

        result_seasons.append(
            {
                "tvmaze_season_id": season_id,
                "season_number": season_number,
                "name": season.get("name", "") or f"Season {season_number}",
                "episode_count": season.get("episodeOrder") or len(episodes),
                "poster_url": (
                    season.get("image", {}).get("medium")
                    if season.get("image")
                    else None
                ),
                "episodes": episodes,
            }
        )

    return jsonify({"tvmaze_id": tvmaze_id, "seasons": result_seasons})


def _tvmaze_fallback(tmdb_id):
    """Return TMDB season data in TVmaze-compatible format as fallback.

    Uses synthetic negative tvmaze_episode_ids to allow watched tracking
    even when TVmaze doesn't have the show. Synthetic ID encodes:
      -(tmdb_id * 10000000 + season_number * 10000 + episode_number)
    """
    try:
        detail_data = tmdb_get_json(
            f"{TMDB_BASE_URL}/tv/{tmdb_id}",
            {"api_key": TMDB_API_KEY, "language": "es-ES"},
        )
        tmdb_seasons = detail_data.get("seasons", [])
        if not tmdb_seasons:
            return jsonify({"tvmaze_id": 0, "seasons": []})

        user_id = get_user_id()
        watched_fallback = set()
        if user_id:
            conn2 = get_db()
            rows = conn2.execute(
                "SELECT tvmaze_episode_id FROM episode_tracking WHERE user_id = ? AND tvmaze_episode_id < 0 AND watched = 1",
                (user_id,),
            ).fetchall()
            watched_fallback = {r["tvmaze_episode_id"] for r in rows}
            conn2.close()

        result_seasons = []
        for s in tmdb_seasons:
            season_number = s.get("season_number", 0)
            if season_number == 0:
                continue

            episodes = []
            try:
                season_data = tmdb_get_json(
                    f"{TMDB_BASE_URL}/tv/{tmdb_id}/season/{season_number}",
                    {"api_key": TMDB_API_KEY, "language": "es-ES"},
                )
                for ep in season_data.get("episodes", []):
                    ep_num = ep.get("episode_number")
                    if ep_num is None:
                        continue
                    syn_id = -(tmdb_id * 10000000 + season_number * 10000 + ep_num)
                    episodes.append(
                        {
                            "tvmaze_episode_id": syn_id,
                            "season_number": season_number,
                            "episode_number": ep_num,
                            "name": ep.get("name", ""),
                            "vote_average": ep.get("vote_average"),
                            "still_url": (
                                f"https://image.tmdb.org/t/p/w300{ep['still_path']}"
                                if ep.get("still_path")
                                else None
                            ),
                            "air_date": ep.get("air_date", ""),
                            "runtime": ep.get("runtime"),
                            "summary": ep.get("overview", ""),
                            "watched": syn_id in watched_fallback,
                        }
                    )
            except Exception:
                pass

            result_seasons.append(
                {
                    "tvmaze_season_id": 0,
                    "season_number": season_number,
                    "name": s.get("name", "") or f"Temporada {season_number}",
                    "episode_count": s.get("episode_count", 0),
                    "poster_url": (
                        f"https://image.tmdb.org/t/p/w200{s['poster_path']}"
                        if s.get("poster_path")
                        else None
                    ),
                    "episodes": episodes,
                }
            )

        return jsonify({"tvmaze_id": 0, "seasons": result_seasons})
    except Exception:
        return jsonify({"tvmaze_id": 0, "seasons": []})


@app.route("/tracking/episode/<int:tvmaze_episode_id>/toggle", methods=["POST"])
def toggle_episode_watched(tvmaze_episode_id):
    user_id = get_user_id()
    if user_id is None:
        return jsonify({"status": 401, "message": "No autenticado"}), 401

    data = request.json or {}
    conn = get_db()

    existing = conn.execute(
        "SELECT id, watched FROM episode_tracking WHERE user_id = ? AND tvmaze_episode_id = ?",
        (user_id, tvmaze_episode_id),
    ).fetchone()

    if existing:
        new_watched = 0 if existing["watched"] else 1
        conn.execute(
            "UPDATE episode_tracking SET watched = ?, watched_at = CASE WHEN ? THEN datetime('now') ELSE NULL END WHERE id = ?",
            (new_watched, new_watched, existing["id"]),
        )
        conn.commit()
        conn.close()
        return jsonify({"status": 200, "watched": bool(new_watched)})
    else:
        conn.execute(
            """INSERT INTO episode_tracking (user_id, tvmaze_episode_id, season_number, episode_number, show_title, watched, watched_at)
               VALUES (?, ?, ?, ?, ?, 1, datetime('now'))""",
            (
                user_id,
                tvmaze_episode_id,
                data.get("season_number", 0),
                data.get("episode_number", 0),
                data.get("show_title", ""),
            ),
        )
        conn.commit()
        conn.close()
        return jsonify({"status": 200, "watched": True})


@app.route("/tracking/batch", methods=["POST"])
def toggle_episode_batch():
    user_id = get_user_id()
    if user_id is None:
        return jsonify({"status": 401, "message": "No autenticado"}), 401

    data = request.json or {}
    episodes = data.get("episodes", [])
    watched = data.get("watched", True)

    conn = get_db()
    now = datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S")
    for ep in episodes:
        tvmaze_id = ep["tvmaze_episode_id"]
        existing = conn.execute(
            "SELECT id FROM episode_tracking WHERE user_id = ? AND tvmaze_episode_id = ?",
            (user_id, tvmaze_id),
        ).fetchone()

        if existing:
            conn.execute(
                "UPDATE episode_tracking SET watched = ?, watched_at = CASE WHEN ? THEN ? ELSE NULL END WHERE id = ?",
                (1 if watched else 0, 1 if watched else 0, now, existing["id"]),
            )
        else:
            if watched:
                conn.execute(
                    """INSERT INTO episode_tracking (user_id, tvmaze_episode_id, season_number, episode_number, show_title, watched, watched_at)
                       VALUES (?, ?, ?, ?, ?, 1, ?)""",
                    (
                        user_id,
                        tvmaze_id,
                        ep.get("season_number", 0),
                        ep.get("episode_number", 0),
                        ep.get("show_title", ""),
                        now,
                    ),
                )
    conn.commit()
    conn.close()
    return jsonify({"status": 200})


# ─── TMDB Vote ────────────────────────────────────────────────────────────────


def get_or_create_guest_session(user_id):
    conn = get_db()
    row = conn.execute(
        "SELECT tmdb_guest_session_id FROM users WHERE id = ?", (user_id,)
    ).fetchone()
    if row and row["tmdb_guest_session_id"]:
        conn.close()
        return row["tmdb_guest_session_id"]
    try:
        resp = requests.get(
            f"{TMDB_BASE_URL}/authentication/guest_session/new",
            params={"api_key": TMDB_API_KEY},
        )
        if resp.ok:
            gs_id = resp.json()["guest_session_id"]
            conn.execute(
                "UPDATE users SET tmdb_guest_session_id = ? WHERE id = ?",
                (gs_id, user_id),
            )
            conn.commit()
            conn.close()
            return gs_id
    except Exception:
        pass
    conn.close()
    return None


@app.route("/vote", methods=["POST"])
def tmdb_vote():
    user_id = get_user_id()
    if user_id is None:
        return jsonify({"status": 401, "message": "No autenticado"}), 401

    data = request.json or {}
    media_type = data.get("media_type")
    tmdb_id = data.get("tmdb_id")
    value = data.get("value")

    if not media_type or not tmdb_id:
        return jsonify({"status": 400, "message": "Faltan campos"}), 400

    gs_id = get_or_create_guest_session(user_id)
    if not gs_id:
        return jsonify({"status": 500, "message": "No se pudo crear sesión TMDB"}), 500

    try:
        if value is not None:
            resp = requests.post(
                f"{TMDB_BASE_URL}/{media_type}/{tmdb_id}/rating",
                params={"api_key": TMDB_API_KEY, "guest_session_id": gs_id},
                json={"value": value},
            )
        else:
            resp = requests.delete(
                f"{TMDB_BASE_URL}/{media_type}/{tmdb_id}/rating",
                params={"api_key": TMDB_API_KEY, "guest_session_id": gs_id},
            )
        return jsonify({"status": resp.status_code, "success": resp.ok})
    except requests.RequestException:
        return jsonify({"status": 500, "message": "Error al conectar con TMDB"}), 500


# ─── Auth helper ────────────────────────────────────────────────────────────


def get_user_id():
    user_id = session.get("user_id")
    if user_id is not None:
        return user_id
    auth = request.headers.get("Authorization", "")
    if auth.startswith("Bearer "):
        try:
            data = token_serializer.loads(auth[7:], max_age=TOKEN_MAX_AGE)
            return data["user_id"]
        except Exception:
            pass
    return None


# ─── Lists ─────────────────────────────────────────────────────────────────


@app.route("/lists", methods=["GET", "POST"])
def handle_lists():
    user_id = get_user_id()
    if user_id is None:
        return jsonify({"status": 401, "message": "No autenticado"}), 401
    conn = get_db()

    if request.method == "GET":
        rows = conn.execute(
            """
            SELECT l.*,
                (SELECT COUNT(*) FROM list_items li WHERE li.list_id = l.id) AS item_count,
                (SELECT li2.poster_path FROM list_items li2 WHERE li2.list_id = l.id ORDER BY li2.added_at ASC LIMIT 1) AS first_item_poster
            FROM lists l
            WHERE l.user_id = ?
            ORDER BY
                CASE l.list_type
                    WHEN 'watchlist' THEN 0
                    WHEN 'watched' THEN 1
                    WHEN 'liked' THEN 2
                    ELSE 3
                END,
                l.created_at ASC
            """,
            (user_id,),
        ).fetchall()
        conn.close()
        return jsonify([dict(r) for r in rows])

    if request.method == "POST":
        data = request.json
        name = (data.get("name") or "").strip()
        if not name:
            conn.close()
            return jsonify({"status": 400, "message": "Nombre requerido"}), 400

        description = (data.get("description") or "").strip()
        cursor = conn.execute(
            "INSERT INTO lists (user_id, name, description, list_type) VALUES (?, ?, ?, 'custom')",
            (user_id, name, description),
        )
        conn.commit()
        list_id = cursor.lastrowid
        row = conn.execute("SELECT * FROM lists WHERE id = ?", (list_id,)).fetchone()
        conn.close()
        return jsonify(dict(row))


@app.route("/lists/<int:list_id>", methods=["PATCH", "DELETE"])
def handle_list(list_id):
    user_id = get_user_id()
    if user_id is None:
        return jsonify({"status": 401, "message": "No autenticado"}), 401
    conn = get_db()
    row = conn.execute(
        "SELECT * FROM lists WHERE id = ? AND user_id = ?", (list_id, user_id)
    ).fetchone()

    if not row:
        conn.close()
        return jsonify({"status": 404, "message": "Lista no encontrada"}), 404

    if request.method == "PATCH":
        data = request.json
        name = data["name"].strip() if data.get("name") else row["name"]
        description = data.get("description", "").strip() if "description" in data else row["description"]

        conn.execute(
            "UPDATE lists SET name = ?, description = ? WHERE id = ?",
            (name, description, list_id),
        )
        conn.commit()
        conn.close()
        return jsonify({"status": 200})

    if request.method == "DELETE":
        if row["list_type"] != "custom":
            conn.close()
            return (
                jsonify(
                    {
                        "status": 400,
                        "message": "No se puede eliminar una lista por defecto",
                    }
                ),
                400,
            )

        conn.execute("DELETE FROM lists WHERE id = ?", (list_id,))
        conn.commit()
        conn.close()
        return jsonify({"status": 200})


# ─── List Items ─────────────────────────────────────────────────────────────


@app.route("/lists/<int:list_id>/items", methods=["GET", "POST"])
def handle_list_items(list_id):
    user_id = get_user_id()
    if user_id is None:
        return jsonify({"status": 401, "message": "No autenticado"}), 401
    conn = get_db()

    list_row = conn.execute(
        "SELECT * FROM lists WHERE id = ? AND user_id = ?", (list_id, user_id)
    ).fetchone()
    if not list_row:
        conn.close()
        return jsonify({"status": 404, "message": "Lista no encontrada"}), 404

    if request.method == "GET":
        media_type = request.args.get("media_type")
        if media_type in ("movie", "tv"):
            items = conn.execute(
                "SELECT * FROM list_items WHERE list_id = ? AND media_type = ? ORDER BY added_at DESC",
                (list_id, media_type),
            ).fetchall()
        else:
            items = conn.execute(
                "SELECT * FROM list_items WHERE list_id = ? ORDER BY added_at DESC",
                (list_id,),
            ).fetchall()
        conn.close()
        return jsonify([dict(i) for i in items])

    if request.method == "POST":
        data = request.json
        try:
            cursor = conn.execute(
                """
                INSERT INTO list_items (list_id, tmdb_id, media_type, title, poster_path, vote_average)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (
                    list_id,
                    data["tmdb_id"],
                    data["media_type"],
                    data["title"],
                    data.get("poster_path"),
                    data.get("vote_average"),
                ),
            )
            conn.commit()
            item_id = cursor.lastrowid
            item = conn.execute(
                "SELECT * FROM list_items WHERE id = ?", (item_id,)
            ).fetchone()
            conn.close()
            return jsonify(dict(item))
        except sqlite3.IntegrityError:
            conn.close()
            return jsonify({"status": 409, "message": "Ya está en esta lista"}), 409


@app.route("/lists/<int:list_id>/items/<int:item_id>", methods=["DELETE"])
def handle_list_item(list_id, item_id):
    user_id = get_user_id()
    if user_id is None:
        return jsonify({"status": 401, "message": "No autenticado"}), 401
    conn = get_db()

    list_row = conn.execute(
        "SELECT * FROM lists WHERE id = ? AND user_id = ?", (list_id, user_id)
    ).fetchone()
    if not list_row:
        conn.close()
        return jsonify({"status": 404, "message": "Lista no encontrada"}), 404

    item = conn.execute(
        "SELECT * FROM list_items WHERE id = ? AND list_id = ?",
        (item_id, list_id),
    ).fetchone()
    if not item:
        conn.close()
        return jsonify({"status": 404, "message": "Item no encontrado"}), 404

    conn.execute("DELETE FROM list_items WHERE id = ?", (item_id,))
    conn.commit()
    conn.close()
    return jsonify({"status": 200})


# ─── Watchlist (legacy) ─────────────────────────────────────────────────────


@app.route("/watchlist", methods=["GET", "POST"])
def watchlist():
    user_id = get_user_id()
    if user_id is None:
        return jsonify({"status": 401, "message": "No autenticado"}), 401
    conn = get_db()

    if request.method == "GET":
        status_filter = request.args.get("status")
        if status_filter in ("pending", "watched"):
            items = conn.execute(
                "SELECT * FROM watchlist WHERE user_id = ? AND status = ? ORDER BY added_at DESC",
                (user_id, status_filter),
            ).fetchall()
        else:
            items = conn.execute(
                "SELECT * FROM watchlist WHERE user_id = ? ORDER BY added_at DESC",
                (user_id,),
            ).fetchall()
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
            return jsonify({"status": 409, "message": "Ya está en tu watchlist"}), 409


@app.route("/watchlist/<int:item_id>", methods=["PATCH", "DELETE"])
def watchlist_item(item_id):
    user_id = get_user_id()
    if user_id is None:
        return jsonify({"status": 401, "message": "No autenticado"}), 401
    conn = get_db()
    item = conn.execute(
        "SELECT * FROM watchlist WHERE id = ? AND user_id = ?", (item_id, user_id)
    ).fetchone()

    if not item:
        conn.close()
        return jsonify({"status": 404, "message": "Item no encontrado"}), 404

    if request.method == "PATCH":
        data = request.json
        status = data.get("status", item["status"])
        rating = data.get("rating", item["rating"])
        notes = data.get("notes", item["notes"])

        if "status" in data and data["status"] == "watched":
            conn.execute(
                "UPDATE watchlist SET status = ?, rating = ?, notes = ?, watched_at = CURRENT_TIMESTAMP WHERE id = ?",
                (status, rating, notes, item_id),
            )
        else:
            conn.execute(
                "UPDATE watchlist SET status = ?, rating = ?, notes = ? WHERE id = ?",
                (status, rating, notes, item_id),
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
