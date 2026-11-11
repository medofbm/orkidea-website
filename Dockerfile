FROM mcr.microsoft.com/dotnet/sdk@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /src
COPY global.json ./
COPY Orkideya/Orkideya.csproj Orkideya/
RUN dotnet restore Orkideya/Orkideya.csproj -p:NuGetAudit=true -p:NuGetAuditMode=all -warnaserror:NU1901,NU1902,NU1903,NU1904
COPY Orkideya/ Orkideya/
RUN dotnet publish Orkideya/Orkideya.csproj -c Release --no-restore --no-self-contained -p:UseAppHost=false --warnaserror -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS runtime
WORKDIR /app
COPY --from=build /out/ ./
RUN mkdir -p /var/orkidea/keys /app/wwwroot/images/products && chown -R "$APP_UID" /var/orkidea /app/wwwroot/images/products && chmod 700 /var/orkidea/keys
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 CMD ["/bin/bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/ready HTTP/1.1\\r\\nHost: 127.0.0.1\\r\\nConnection: close\\r\\n\\r\\n' >&3 && read -r protocol status reason <&3 && [[ $protocol == HTTP/1.* && $status == 200 ]]"]
ENTRYPOINT ["dotnet", "Orkideya.dll"]
