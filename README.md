# Introduction
This is a POC how to bind Camunda *User Task* with c# Worker deployed on k8s (Rancher).

## Step 1 : Worker
Use *Microsoft.NET.Sdk.Worker* to create a *BackgroundService* 
https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service

## Step 2 : Model Camunda (Bpmn)
A simple modele having a *Task element* as *User Task*. This one must set JobType like defined in Worker.

## Step 3 : Helm
...