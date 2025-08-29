# NICETaskDafna.Api
Simple ASP.NET Core 8 Web API that suggests a task based on a user utterance.

## How to run
**Prerequisite:** .NET 8 SDK
### Restore & Build
dotnet restore  
dotnet build
### Run the API
dotnet run --project NICETaskDafna.Api.csproj
## Run tests
From the repo root:  
dotnet test .\tests\NICETaskDafna.Api.Tests\NICETaskDafna.Api.Tests.csproj --logger "console;verbosity=detailed"


##Run API: 
The console will print the listening URL (e.g. http://localhost:5196).  
### Swagger UI
Once the API is running, open Swagger in your browser at:  
http://localhost:<port>/swagger

From there you can:
1. Expand the `POST /suggestTask` endpoint.
2. Click **Try it out**.
3. Paste an example JSON body, for example:
   ```json
   {
     "utterance": "I forgot my password",
     "userId": "u1",
     "sessionId": "s1",
     "timestamp": "2025-08-21T12:00:00Z"
   }
