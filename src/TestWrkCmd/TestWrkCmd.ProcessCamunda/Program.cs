using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Zeebe.Client;

[assembly: ExcludeFromCodeCoverage]

var zeebeClient = ZeebeClient
    .Builder()
    .UseGatewayAddress("localhost:26500")
    .UsePlainText() // Pas de TLS via ngrok TCP
    .Build();

var processInstance = await zeebeClient
    .NewCreateProcessInstanceCommand()
    .BpmnProcessId("Process_07zn7bo")
    .LatestVersion()
    .Send();

await zeebeClient
    .NewSetVariablesCommand(processInstance.ProcessInstanceKey)
    .Variables(JsonSerializer.Serialize(new { processInstanceKey = processInstance.ProcessInstanceKey.ToString() }))
    .SendWithRetry();