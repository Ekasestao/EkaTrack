# Contexto del proyecto — Watchlist App

## Descripción

Aplicación web personal de seguimiento de películas y series. Permite gestionar una watchlist, registrar títulos vistos y añadir valoraciones personales. Uso personal, un solo usuario.

---

## Stack tecnológico

### Frontend

- **Blazor WebAssembly (.NET)**
- Corre íntegramente en el navegador del usuario (compilado a WebAssembly)
- Componentes `.razor` para vistas: watchlist, búsqueda, detalle de película, ratings
- Comunicación con el backend exclusivamente via `HttpClient` (llamadas HTTP/JSON)
- Sin acceso directo a base de datos ni al sistema de archivos

### Backend / API

- **Flask (Python)**
- API REST ligera que actúa de intermediario entre el frontend y TMDB
- Oculta la API key de TMDB — nunca se expone al cliente
- Gestiona las operaciones de lectura/escritura sobre la base de datos
- CORS habilitado con `flask-cors` para permitir peticiones desde el dominio de Firebase

### Base de datos

- **SQLite**
- Disponible para todas las cuentas de PythonAnywhere incluidas las nuevas gratuitas
- Un único archivo `.db` alojado en el sistema de archivos de PythonAnywhere
- Acceso desde Flask con `flask-sqlalchemy` o el módulo `sqlite3` de la librería estándar
- Adecuado para uso personal de un solo usuario — las advertencias de rendimiento de PythonAnywhere aplican a apps con mucho tráfico concurrente

### API externa

- **TMDB (The Movie Database)**
- Proporciona metadatos de películas y series: título, póster, géneros, sinopsis, reparto, ratings
- Gratuita para uso no comercial
- Rate limit: ~40 requests/segundo por IP (más que suficiente para uso personal)
- Requiere mostrar el logo de TMDB como atribución
- La API key se guarda como variable de entorno en PythonAnywhere, nunca en el código

---

## Hosting

| Capa                          | Servicio         | Plan            | Coste  |
| ----------------------------- | ---------------- | --------------- | ------ |
| Frontend (Blazor WASM)        | Firebase Hosting | Spark (free)    | Gratis |
| Backend + BD (Flask + SQLite) | PythonAnywhere   | Beginner (free) | Gratis |
| API externa                   | TMDB             | Developer       | Gratis |

---

## Arquitectura

```
Browser
  └── Blazor WASM (Firebase Hosting)
        └── HttpClient
              └── Flask API (PythonAnywhere)
                    ├── SQLite — watchlist.db (PythonAnywhere)
                    └── TMDB API (externa)
```

---

## Funcionalidades previstas

- Buscar películas y series por título (via TMDB)
- Ver detalle de una película: póster, sinopsis, géneros, reparto
- Añadir títulos a la watchlist (pendiente de ver)
- Marcar títulos como vistos
- Añadir valoración personal (puntuación y/o notas)
- Listar watchlist con filtros (pendiente / visto)

---

## Decisiones técnicas relevantes

**¿Por qué Blazor WASM y no Blazor Server?**
Blazor Server requiere un servidor .NET corriendo continuamente con conexiones SignalR activas. Firebase Hosting solo sirve archivos estáticos y PythonAnywhere solo ejecuta Python, por lo que Blazor Server no es compatible con este stack. Blazor WASM se despliega como un sitio estático y es la opción correcta aquí.

**¿Por qué Flask y no una API .NET?**
PythonAnywhere no soporta .NET. Flask es la opción natural dado que PythonAnywhere es el hosting de backend elegido, y es suficiente para los endpoints que necesita esta aplicación.

**¿Por qué SQLite y no MySQL?**
Las cuentas nuevas de PythonAnywhere (creadas desde enero de 2026) ya no incluyen MySQL en el plan gratuito. SQLite está disponible para todas las cuentas y es perfectamente válido para este proyecto: uso personal, un solo usuario, sin escrituras concurrentes. El archivo `.db` vive en el sistema de archivos de PythonAnywhere junto a la API Flask.

**¿Por qué Firebase Hosting?**
Ya utilizado en proyectos anteriores del mismo desarrollador. Despliegue sencillo via CLI (`firebase deploy`), CDN global incluido, HTTPS automático, plan Spark gratuito sin límite de tiempo.

---

## Diferencias clave entre Blazor WASM y Server (resumen)

| Aspecto            | WASM (este proyecto) | Server                           |
| ------------------ | -------------------- | -------------------------------- |
| Dónde corre C#     | En el browser        | En el servidor                   |
| Acceso a BD        | Solo via HTTP        | Directo (inyección de DbContext) |
| API key TMDB       | Segura en Flask      | Segura en el servidor .NET       |
| Hosting compatible | Estático (Firebase)  | Servidor .NET activo             |
| Sintaxis `.razor`  | Idéntica             | Idéntica                         |
