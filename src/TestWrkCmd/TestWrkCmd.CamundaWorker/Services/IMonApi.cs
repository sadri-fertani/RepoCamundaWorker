using Refit;
using TestWrkCmd.Common.Payloads;

namespace TestWrkCmd.CamundaWorker.Services;

public interface IMonApi
{
    [Get("/hello")]
    Task<RespApi> GetDataAsync();
}