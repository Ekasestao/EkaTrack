import os

secret_key = os.environ.get("SECRET_KEY")
bearer_token = os.environ.get("TMDB_BEARER_TOKEN")
api_key = os.environ.get("TMDB_API_KEY")
api_url = os.environ.get("TMDB_API_URL")
api_url_tvmaze = os.environ.get("TVMAZE_API_URL")
database = os.environ.get("DATABASE")
