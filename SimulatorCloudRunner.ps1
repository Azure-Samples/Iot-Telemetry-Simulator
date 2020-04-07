
param (
    [string]$Location = "westeurope",
    [string]$ResourceGroup = "iothubsimulator",
    [int]$DeviceCount = 100,
    [int]$ContainerCount = 1,
    [int]$MessageCount = 100,
    [int]$Interval = 1000,
    [string]$FixPayload='',
    [int]$FixPayloadSize=0,
    [string]$Template = '{ \"deviceId\": \"$.DeviceId\", \"temp\": $.Temp, \"Ticks\": $.Ticks, \"Counter\": $.Counter, \"time\": \"$.Time\", \"engine\": \"$.Engine\", \"source\": \"$.MachineName\" }',
    [string]$Header = '',
    [string]$Variables = '[{name: \"Temp\", random: true, max: 25, min: 23}, {name:\"Counter\", min:100}, {name:\"Engine\", values: [\"on\", \"off\"]}]',
    [Parameter(Mandatory=$true)][string]$IotHubConnectionString,
    [string]$Image = "iottelemetrysimulator/azureiot-telemetrysimulator:latest",
    [double]$Cpu = 1.0,
    [double]$Memory = 1.5
 )

 az group create --name $ResourceGroup --location $Location

 $i = 0
 $deviceIndex = 1
 $devicesPerContainer = [int]($DeviceCount / $ContainerCount)
 while($i -lt $ContainerCount)
 {
    $i++
    $containerName = "iotsimulator-" + $i.ToString()
    az container create -g $ResourceGroup --no-wait --location $Location --restart-policy Never --cpu $Cpu --memory $Memory --name $containerName --image $Image --environment-variables IotHubConnectionString=$IotHubConnectionString Template=$Template Variables=$Variables DeviceCount=$devicesPerContainer MessageCount=$MessageCount DeviceIndex=$deviceIndex Interval=$Interval Header=$Header FixPayloadSize=$FixPayloadSize FixPayload=$FixPayload

    $deviceIndex = $deviceIndex + $devicesPerContainer
 }

 Write-Host "Creation of" $ContainerCount "container instances has started. Telemetry will start flowing soon"