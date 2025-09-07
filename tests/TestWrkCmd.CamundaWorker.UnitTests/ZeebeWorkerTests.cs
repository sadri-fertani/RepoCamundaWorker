using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics.CodeAnalysis;
using TestWrkCmd.CamundaWorker.Options;
using TestWrkCmd.CamundaWorker.Payloads;
using TestWrkCmd.CamundaWorker.Services;
using Zeebe.Client;
using Zeebe.Client.Api.Commands;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace TestWrkCmd.CamundaWorker.UnitTests;

[ExcludeFromCodeCoverage]
public class ZeebeWorkerTests
{
    private readonly Mock<IMonApi> _monApiMock = new();
    private readonly Mock<ILogger<ZeebeWorker>> _loggerMock = new();
    private readonly ZeebeOptions _zeebeOptions = new() { GatewayAddress = "fake.localhost:26500" };
    private readonly WorkerOptions _workerOptions = new()
    {
        JobType = "test-job",
        MaxJobActive = 5,
        WorkerName = "test-worker",
        PollInterval = 1,
        Timeout = 1
    };

    private ZeebeWorker CreateWorker()
    {
        var zeebeOptionsMock = new OptionsWrapper<ZeebeOptions>(_zeebeOptions);
        var workerOptionsMock = new OptionsWrapper<WorkerOptions>(_workerOptions);

        return new ZeebeWorker(
            _monApiMock.Object,
            _loggerMock.Object,
            zeebeOptionsMock,
            workerOptionsMock
        );
    }

    [Fact]
    public void Constructor_Should_Throw_If_Arguments_Null()
    {
        var zeebeOptionsMock = new OptionsWrapper<ZeebeOptions>(_zeebeOptions);
        var workerOptionsMock = new OptionsWrapper<WorkerOptions>(_workerOptions);

        Assert.Throws<ArgumentNullException>(() => new ZeebeWorker(null, _loggerMock.Object, zeebeOptionsMock, workerOptionsMock));
        Assert.Throws<ArgumentNullException>(() => new ZeebeWorker(_monApiMock.Object, null, zeebeOptionsMock, workerOptionsMock));
        Assert.Throws<ArgumentNullException>(() => new ZeebeWorker(_monApiMock.Object, _loggerMock.Object, null, workerOptionsMock));
        Assert.Throws<ArgumentNullException>(() => new ZeebeWorker(_monApiMock.Object, _loggerMock.Object, zeebeOptionsMock, null));
    }

    [Fact]
    public async Task ExecuteAsync_Should_Return_CompletedTask_When_No_Exception()
    {
        // Arrange
        var worker = CreateWorker();

        // Act
        var task = worker.StartAsync(CancellationToken.None);
        await task.WaitAsync(CancellationToken.None);

        // Assert
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_Should_LogError_And_ReturnFaultedTask_When_NewWorkerThrowsException()
    {
        // Arrange
        var zeebeOptionsMock = new OptionsWrapper<ZeebeOptions>(_zeebeOptions);
        var workerOptionsMock = new OptionsWrapper<WorkerOptions>(_workerOptions);

        var loggerMock = new Mock<ILogger<ZeebeWorker>>();
        var monApiMock = new Mock<IMonApi>();

        var simulatedException = new InvalidOperationException("Simulated failure");

        // Mock du ZeebeClient qui jette une exception dès NewWorker()
        var zeebeClientMock = new Mock<IZeebeClient>();
        zeebeClientMock
            .Setup(z => z.NewWorker())
            .Throws(simulatedException);

        // Création du worker avec injection du client mocké
        var worker = new ZeebeWorker(monApiMock.Object, loggerMock.Object, zeebeOptionsMock, workerOptionsMock);

        // Injection du client Zeebe mocké via réflexion
        typeof(ZeebeWorker)
            .GetField("_zeebeClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(worker, zeebeClientMock.Object);

        // Act
        var resultTask = worker.StartAsync(CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => resultTask);
        Assert.Equal("Simulated failure", ex.Message);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("An error occurred while starting the Zeebe worker.")),
                simulatedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleJobAsync_Should_Complete_Job_With_Expected_Variables()
    {
        // Arrange
        var worker = CreateWorker();

        var jobClientMock = new Mock<IJobClient>();
        var activatedJobMock = new Mock<IJob>();

        activatedJobMock
            .Setup(j => j.Key)
            .Returns(123);

        activatedJobMock
            .Setup(j => j.Variables)
            .Returns("{}");

        var resultApi = new MonApiPayload
        {
            Hostname = "host123",
            ApplicationName = "appXYZ"
        };

        _monApiMock
            .Setup(api => api.GetDataAsync())
            .ReturnsAsync(resultApi);

        // Mock distinct pour l'objet retourné par Variables()
        var variablesStepMock = new Mock<ICompleteJobCommandStep1>();
        
        var finalStepMock = variablesStepMock.As<IFinalCommandStep<ICompleteJobResponse>>();
        finalStepMock
            .Setup(f => f.Send(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ICompleteJobResponse>().Object);

        // Mock de la première étape de commande
        var commandStepMock = new Mock<ICompleteJobCommandStep1>();
        commandStepMock
            .Setup(c => c.Variables(It.IsAny<string>()))
            .Returns(variablesStepMock.Object); // retourne le mock enrichi

        jobClientMock
            .Setup(c => c.NewCompleteJobCommand(123))
            .Returns(commandStepMock.Object);

        // Act
        var handleJobAsyncMethod = typeof(ZeebeWorker).GetMethod("HandleJobAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = handleJobAsyncMethod!.Invoke(worker, [jobClientMock.Object, activatedJobMock.Object]) as Task;
        await task!;

        // Assert
        _monApiMock.Verify(api => api.GetDataAsync(), Times.Once);

        finalStepMock.Verify(f => f.Send(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
