using Task = TasksApi.Models.Task;

namespace TasksApi.Responses
{
    public class SaveTaskResponse : BaseResponse
    {
        public Task Task { get; set; }
    }
}
