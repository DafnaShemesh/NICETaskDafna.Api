var builder = WebApplication.CreateBuilder(args);

// Register framework services.
// Add support for MVC-style controllers (so [ApiController] classes are discovered).
builder.Services.AddControllers();

// OpenAPI/Swagger for easy testing and documentation.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enforce HTTPS for local/dev security.
app.UseHttpsRedirection();

// Route incoming HTTP requests to controller actions.
// This enables attribute routing like [Route("/suggestTask")].
app.MapControllers();

app.Run();
