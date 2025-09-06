using Refit;
using TestWrkCmd.CamundaWorker.Payloads;

namespace TestWrkCmd.CamundaWorker.Services;

public interface IMonApi
{
    [Get("/get-config")]
    Task<MonApiPayload> GetDataAsync();
}

