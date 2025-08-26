using NICETaskDafna.Api.Matching;

var builder = WebApplication.CreateBuilder(args);

//Register services 
builder.Services.AddControllers();
builder.Services.AddScoped<ITaskMatcher, KeywordTaskMatcher>(); // Register our task matcher service
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
