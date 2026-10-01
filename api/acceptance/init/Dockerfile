# One-off set-up of the acceptance database (docs/ACCEPTANCE.md): the EF Core migration bundle, the specification
# scripts, the seed and init.sh, on the PostgreSQL 18 image for psql. Built with api/ as the context.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS bundle
WORKDIR /src
COPY . .
# dotnet ef reads the project metadata before it builds, so the projects are restored first.
RUN dotnet tool restore \
 && dotnet restore src/ZenGear.Api/ZenGear.Api.csproj -r linux-x64 \
 && dotnet ef migrations bundle -p src/ZenGear.Data -s src/ZenGear.Api --configuration Release \
      --self-contained -r linux-x64 -o /out/efbundle

FROM postgres:18
COPY --from=bundle /out/efbundle /init/efbundle
COPY db/ /init/db/
COPY seed/ /init/seed/
COPY --chmod=0755 acceptance/init/init.sh /init/init.sh
USER postgres
ENTRYPOINT ["/init/init.sh"]
