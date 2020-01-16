@echo off

if "%~1"=="" (
    set repo="fbeltrao"
) else (
    set repo=%1
)

if "%~2"=="" (
    set tag="1.0"
) else (
    set tag=%2
)

docker build -f src\IotTelemetrySimulator\Dockerfile -t %repo%/azureiot-telemetrysimulator:latest  .
docker tag %repo%/azureiot-telemetrysimulator:latest %repo%/azureiot-telemetrysimulator:%tag%
docker push %repo%/azureiot-telemetrysimulator:latest && docker push %repo%/azureiot-telemetrysimulator:%tag%

docker build -f src\IotSimulatorDeviceProvisioning\Dockerfile -t %repo%/azureiot-simulatordeviceprovisioning:latest  .
docker tag %repo%/azureiot-simulatordeviceprovisioning:latest %repo%/azureiot-simulatordeviceprovisioning:%tag%
docker push %repo%/azureiot-simulatordeviceprovisioning:latest && docker push %repo%/azureiot-simulatordeviceprovisioning:%tag%