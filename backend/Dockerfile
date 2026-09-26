# Образ серверной части.
#
# Сборка выполняется в два этапа. На первом этапе применяется образ
# с набором средств разработки: в нём восстанавливаются зависимости
# и компилируется приложение. На втором этапе в образ среды выполнения
# копируется только результат компиляции. Средства разработки
# в итоговый образ не попадают: это сокращает его объём в несколько раз
# и уменьшает число содержащихся в нём программ, а значит и число
# возможных уязвимостей.

# ---------------------------------------------------------------------------
# Этап сборки
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Файлы проектов копируются отдельно от исходного кода и прежде него.
# Основание: сборщик образов кеширует результат каждой команды и повторно
# выполняет команду только при изменении входных данных. Перечень
# зависимостей изменяется редко, исходный код — часто. При таком порядке
# изменение кода не приводит к повторной загрузке пакетов из сети.
COPY RiskAnalysis.slnx ./
COPY src/RiskAnalysis.Domain/RiskAnalysis.Domain.csproj                 src/RiskAnalysis.Domain/
COPY src/RiskAnalysis.RiskEngine/RiskAnalysis.RiskEngine.csproj         src/RiskAnalysis.RiskEngine/
COPY src/RiskAnalysis.Application/RiskAnalysis.Application.csproj       src/RiskAnalysis.Application/
COPY src/RiskAnalysis.Infrastructure/RiskAnalysis.Infrastructure.csproj src/RiskAnalysis.Infrastructure/
COPY src/RiskAnalysis.WebApi/RiskAnalysis.WebApi.csproj                 src/RiskAnalysis.WebApi/
COPY tests/RiskAnalysis.RiskEngine.Tests/RiskAnalysis.RiskEngine.Tests.csproj tests/RiskAnalysis.RiskEngine.Tests/

RUN dotnet restore src/RiskAnalysis.WebApi/RiskAnalysis.WebApi.csproj

COPY . .

RUN dotnet publish src/RiskAnalysis.WebApi/RiskAnalysis.WebApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ---------------------------------------------------------------------------
# Этап выполнения
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Установка шрифтов, содержащих начертания букв кириллического алфавита.
#
# Шаг обязателен. Образ среды выполнения шрифтов не содержит, а шрифт Lato,
# поставляемый с библиотекой QuestPDF, кириллических начертаний не имеет:
# без этой установки отчёты формировались бы с неотображаемыми знаками
# вместо русского текста. Ограничение зафиксировано в ADR-0014, настоящая
# команда его устраняет.
#
# Устанавливаются оба семейства, которые ищет ReportFonts: DejaVu Sans
# и Liberation Sans. Перечень пакетов и сопутствующие файлы после
# установки удаляются, чтобы не увеличивать объём образа.
# Вместе со шрифтами устанавливается curl: он требуется для проверки
# работоспособности контейнера, описанной в файле составления.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        fonts-dejavu-core \
        fonts-liberation \
        curl \
    && rm -rf /var/lib/apt/lists/*

# Приложение выполняется от имени непривилегированного пользователя.
# Образ ASP.NET содержит готовую учётную запись app с идентификатором 1654.
# Основание: при выполнении от имени суперпользователя ошибка в приложении
# либо в одной из его зависимостей даёт больше возможностей внутри
# контейнера, чем необходимо для работы.
RUN mkdir -p /app/logs && chown --recursive app:app /app
USER app

COPY --from=build --chown=app:app /app/publish ./

# Приём соединений со всех сетевых интерфейсов контейнера: обращение
# приходит от сервера отдачи статических файлов, то есть извне контейнера.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RiskAnalysis.WebApi.dll"]
