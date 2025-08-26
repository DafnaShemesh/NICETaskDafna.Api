using Microsoft.AspNetCore.Mvc;
using NICETaskDafna.Api.Contracts;
using NICETaskDafna.Api.Matching;

namespace NICETaskDafna.Api.Controllers;

[ApiController]
[Route("/suggestTask")]
public class SuggestTaskController : ControllerBase
{
    private readonly ITaskMatcher _matcher;

    public SuggestTaskController(ITaskMatcher matcher)
    {
        _matcher = matcher;
    }

    [HttpPost]
    public ActionResult<SuggestTaskResponse> Post([FromBody] SuggestTaskRequest request)
    {
        // Use the matcher to find the task
        var task = _matcher.Match(request.Utterance);

        var response = new SuggestTaskResponse(
            Task: task,
            Timestamp: DateTime.UtcNow
        );

        return Ok(response);
    }
}
