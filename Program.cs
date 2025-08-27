using FluentValidation;
using FluentValidation.AspNetCore;
using NICETaskDafna.Api.Contracts.Validation;
using NICETaskDafna.Api.Matching;

var builder = WebApplication.CreateBuilder(args);

// Controllers + FluentValidation (Auto-Validation)
builder.Services
    .AddControllers()
    .AddJsonOptions(_ => { });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<SuggestTaskRequestValidator>();
builder.Services.AddScoped<ITaskMatcher, KeywordTaskMatcher>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
