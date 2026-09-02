# Build context is backend/, so ProjectReferences to Domain/Infrastructure
# resolve. This image only needs DB access (see ServiceCollectionExtensions'
# AddAtlasticaInfrastructure) - the Wikidata/Wikipedia/Phi-3.5 pipeline lives
# in the separate Atlastica.FactsExtractor process, not in the web API, so
# there's no multi-GB model to bake in or mount here.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Atlastica.Domain/Atlastica.Domain.csproj Atlastica.Domain/
COPY Atlastica.Infrastructure/Atlastica.Infrastructure.csproj Atlastica.Infrastructure/
COPY Atlastica.Nlp/Atlastica.Nlp.csproj Atlastica.Nlp/
COPY Atlastica.Api/Atlastica.Api.csproj Atlastica.Api/
RUN dotnet restore Atlastica.Api/Atlastica.Api.csproj
COPY Atlastica.Domain/ Atlastica.Domain/
COPY Atlastica.Infrastructure/ Atlastica.Infrastructure/
COPY Atlastica.Nlp/ Atlastica.Nlp/
COPY Atlastica.Api/ Atlastica.Api/
RUN dotnet publish Atlastica.Api/Atlastica.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "Atlastica.Api.dll"]
