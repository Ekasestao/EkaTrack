using System.Text.Json.Serialization;

namespace EkaWatch.Client.Models;

public class TmdbMediaDetail
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonIgnore]
    public string Title => TitleRaw ?? NameRaw ?? "";

    [JsonPropertyName("title")]
    public string? TitleRaw { get; set; }

    [JsonPropertyName("name")]
    public string? NameRaw { get; set; }

    [JsonPropertyName("overview")]
    public string Overview { get; set; } = "";

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string? BackdropPath { get; set; }

    [JsonPropertyName("vote_average")]
    public double VoteAverage { get; set; }

    [JsonPropertyName("vote_count")]
    public int VoteCount { get; set; }

    [JsonPropertyName("origin_country")]
    public List<string> OriginCountry { get; set; } = [];

    [JsonPropertyName("production_countries")]
    public List<TmdbProductionCountry> ProductionCountries { get; set; } = [];

    [JsonPropertyName("genres")]
    public List<TmdbGenre> Genres { get; set; } = [];

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("first_air_date")]
    public string? FirstAirDate { get; set; }

    [JsonIgnore]
    public string? ReleaseDateDisplay => ReleaseDate ?? FirstAirDate;

    [JsonPropertyName("runtime")]
    public int? Runtime { get; set; }

    [JsonPropertyName("episode_run_time")]
    public List<int>? EpisodeRunTime { get; set; }

    [JsonPropertyName("last_episode_to_air")]
    public TmdbEpisode? LastEpisodeToAir { get; set; }

    [JsonIgnore]
    public int? DisplayRuntime
    {
        get
        {
            if (Runtime.GetValueOrDefault() > 0) return Runtime;
            if (EpisodeRunTime is { Count: > 0 } && EpisodeRunTime[0] > 0) return EpisodeRunTime[0];
            if (LastEpisodeToAir?.Runtime.GetValueOrDefault() > 0) return LastEpisodeToAir.Runtime;
            return null;
        }
    }

    [JsonPropertyName("tagline")]
    public string Tagline { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonIgnore]
    public string StatusDisplay
    {
        get
        {
            var map = new Dictionary<string, string>
            {
                {"Returning Series", "En emisión"}, {"Ended", "Finalizada"},
                {"Cancelled", "Cancelada"}, {"In Production", "En producción"},
                {"Released", "Estrenada"}, {"Planned", "Planificada"},
                {"Pilot", "Piloto"}, {"Post Production", "Post-producción"},
            };
            return map.TryGetValue(Status, out var s) ? s : Status;
        }
    }

    [JsonPropertyName("networks")]
    public List<TmdbNetwork> Networks { get; set; } = [];

    [JsonPropertyName("production_companies")]
    public List<TmdbNetwork> ProductionCompanies { get; set; } = [];

    [JsonIgnore]
    public string? CompanyDisplay
    {
        get
        {
            var names = (Networks.Count > 0 ? Networks : ProductionCompanies)
                .Select(n => n.Name).ToList();
            if (names.Count <= 3) return string.Join(", ", names);
            return string.Join(", ", names.Take(3)) + $" y {names.Count - 3} más";
        }
    }

    [JsonPropertyName("credits")]
    public TmdbCredits? Credits { get; set; }

    [JsonPropertyName("certification")]
    public string? Certification { get; set; }

    [JsonPropertyName("trailer")]
    public TmdbTrailerInfo? Trailer { get; set; }

    [JsonPropertyName("providers")]
    public TmdbProviderData? Providers { get; set; }

    [JsonPropertyName("recommendations")]
    public List<TmdbRecommendationItem> Recommendations { get; set; } = [];

    [JsonIgnore]
    public string? DirectorName => Credits?.Crew?.FirstOrDefault(c => c.Job == "Director")?.Name;

    [JsonIgnore]
    public string CountryDisplay
    {
        get
        {
            var names = new Dictionary<string, string>
            {
                {"US", "Estados Unidos"}, {"GB", "Reino Unido"}, {"ES", "España"},
                {"FR", "Francia"}, {"DE", "Alemania"}, {"IT", "Italia"},
                {"JP", "Japón"}, {"KR", "Corea del Sur"}, {"CN", "China"},
                {"IN", "India"}, {"CA", "Canadá"}, {"AU", "Australia"},
                {"BR", "Brasil"}, {"MX", "México"}, {"AR", "Argentina"},
                {"RU", "Rusia"}, {"SE", "Suecia"}, {"DK", "Dinamarca"},
                {"NO", "Noruega"}, {"FI", "Finlandia"}, {"NL", "Países Bajos"},
                {"BE", "Bélgica"}, {"CH", "Suiza"}, {"AT", "Austria"},
                {"PT", "Portugal"}, {"PL", "Polonia"}, {"CZ", "República Checa"},
                {"IE", "Irlanda"}, {"NZ", "Nueva Zelanda"}, {"ZA", "Sudáfrica"},
                {"TW", "Taiwán"}, {"HK", "Hong Kong"}, {"TH", "Tailandia"},
                {"IS", "Islandia"}, {"GR", "Grecia"}, {"TR", "Turquía"},
                {"IL", "Israel"}, {"CO", "Colombia"}, {"CL", "Chile"},
                {"PE", "Perú"}, {"CU", "Cuba"}, {"VE", "Venezuela"},
            };
            var codes = new List<string>();
            foreach (var pc in ProductionCountries)
                if (!string.IsNullOrEmpty(pc.Iso3166_1))
                    codes.Add(pc.Iso3166_1);
            foreach (var oc in OriginCountry)
                if (!codes.Contains(oc))
                    codes.Add(oc);
            return string.Join(", ", codes.Select(c => names.TryGetValue(c, out var n) ? n : c));
        }
    }

}

public class TmdbTrailerInfo
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class TmdbProviderData
{
    [JsonPropertyName("flatrate")]
    public List<TmdbProvider>? Flatrate { get; set; }

    [JsonPropertyName("rent")]
    public List<TmdbProvider>? Rent { get; set; }

    [JsonPropertyName("buy")]
    public List<TmdbProvider>? Buy { get; set; }
}

public class TmdbProvider
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class TmdbRecommendationItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; } = "";

    [JsonPropertyName("vote_average")]
    public double VoteAverage { get; set; }
}

public class TmdbGenre
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class TmdbCredits
{
    [JsonPropertyName("cast")]
    public List<TmdbCastMember> Cast { get; set; } = [];

    [JsonPropertyName("crew")]
    public List<TmdbCrewMember> Crew { get; set; } = [];
}

public class TmdbCastMember
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("character")]
    public string Character { get; set; } = "";

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

public class TmdbPaginatedResponse
{
    [JsonPropertyName("results")]
    public List<TmdbMediaItem> Results { get; set; } = [];

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }
}

public class TmdbCrewMember
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("job")]
    public string Job { get; set; } = "";

    [JsonPropertyName("department")]
    public string Department { get; set; } = "";

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; set; }
}

public class TmdbEpisode
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("runtime")]
    public int? Runtime { get; set; }
}

public class TmdbProductionCountry
{
    [JsonPropertyName("iso_3166_1")]
    public string Iso3166_1 { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class TmdbNetwork
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("logo_path")]
    public string? LogoPath { get; set; }

    [JsonPropertyName("origin_country")]
    public string? OriginCountry { get; set; }
}




