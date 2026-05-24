# EkaWatch

Aplicación web de seguimiento de películas y series. Busca títulos en TMDB, organiza listas personalizadas, marca como vistos o pendientes, y descubre nuevos estrenos.

## Stack

| Capa | Tecnología |
|------|-----------|
| Frontend | Blazor WASM (.NET 10, Radzen UI) |
| Backend | Flask (Python) en PythonAnywhere |
| Base de datos | SQLite |
| API externa | TMDB (The Movie Database) |

## Arquitectura

```
Browser → Blazor WASM → HttpClient (X-User-Id header) → Flask API → SQLite + TMDB
```

Auth vía header `X-User-Id` (no cookies cross-origin).

## Funcionalidades

- **Tendencias** — Home con scroll infinito, filtros por Películas/Series, TMDB trending
- **Búsqueda** — Autocomplete en navbar con debounce (300ms), sugerencias TMDB + recientes
- **Detalle** — Póster, backdrop, sinopsis, reparto, géneros, tagline
- **Listas** — Watchlist, Visto, Me gusta (toggle desde detalle) + listas personalizadas
- **Nuevo** — Estrenos (now_playing) y Próximamente (upcoming), agrupados por fecha
- **Recientes** — Últimos 6 títulos visitados guardados en localStorage

## Desarrollo local

### Backend

```bash
cd API
pip install -r requirements.txt  # flask, flask-cors, requests, werkzeug
cp .env.example .env             # editar con tus API keys
python app.py                    # arranca en :5000
```

### Frontend

```bash
cd "APP/Fuentes"
dotnet restore
dotnet run --project EkaWatch   # arranca en :7094 (https)
```

### Tests

```bash
cd "APP/Fuentes/EkaWatch.Tests"
dotnet test
```

(29 tests — ListsService: 12, TmdbService: 17)

## Hosting

| Capa | Servicio | Plan |
|------|----------|------|
| Frontend | ASP.NET Core host | Local / servidor .NET |
| Backend + BD | PythonAnywhere | Beginner (free) |
| API externa | TMDB | Developer (free) |

## Estado del proyecto

Todas las funcionalidades core están implementadas y funcionando. Queda como backlog:

- [ ] Tracking por episodio en series
- [ ] Página de perfil / avatar
