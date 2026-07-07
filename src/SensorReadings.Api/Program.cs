using SensorReadings.Api;
using SensorReadings.Domain;
using SensorReadings.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Sensor Readings API",
        Version = "v1",
        Description = "Ingests sensor readings from a jsonl file, cleans duplicates and invalid " +
                      "records, and exposes time-based aggregation of the data.",
    });

    var xmlDocs = Path.Combine(AppContext.BaseDirectory, "SensorReadings.Api.xml");
    if (File.Exists(xmlDocs))
        options.IncludeXmlComments(xmlDocs);
});

var readingsFile = builder.Configuration["Readings:FilePath"]
    ?? throw new InvalidOperationException("Readings:FilePath is not configured.");
var readingsPath = Path.GetFullPath(readingsFile, builder.Environment.ContentRootPath);

builder.Services.AddSingleton<IReadingSource>(new JsonlReadingSource(readingsPath));
builder.Services.AddSingleton<IReadingRepository, InMemoryReadingRepository>();
builder.Services.AddSingleton<IngestionService>();
builder.Services.AddSingleton<IngestionReportStore>();

var app = builder.Build();

// Ingest the file once at startup; afterwards the API serves everything from the
// repository. The resulting count report stays available at GET /api/ingestion/report.
var report = app.Services.GetRequiredService<IngestionService>().Run();
app.Services.GetRequiredService<IngestionReportStore>().Set(report);

// Swagger is enabled unconditionally: this service is a technical exercise and the
// interactive docs are part of the deliverable.
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
