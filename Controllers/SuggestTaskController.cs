using Microsoft.AspNetCore.Mvc;
using NICETaskDafna.Api.Contracts;

namespace NICETaskDafna.Api.Controllers;

[ApiController]
// The assignment requires the endpoint path to be exactly "/suggestTask".
[Route("/suggestTask")]
public class SuggestTaskController : ControllerBase
{
    // POST /suggestTask
    // Receives a JSON body that matches SuggestTaskRequest (utterance, userId, sessionId, timestamp)
    // and returns a JSON response (task, timestamp).
    [HttpPost]
    public ActionResult<SuggestTaskResponse> Post([FromBody] SuggestTaskRequest request)
    {
        // Phase 1: return a stub so we can verify end-to-end flow in Swagger/Postman.
        // Next steps will add: validation, logging, and real keyword matching.
        var response = new SuggestTaskResponse(
            Task: "StubTask",
            Timestamp: DateTime.UtcNow
        );

        return Ok(response); // HTTP 200 + JSON
    }
}
