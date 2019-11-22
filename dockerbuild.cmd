docker build -f src\IotTelemetrySimulator\Dockerfile -t fbeltrao/azureiot-telemetrysimulator:latest  .
docker tag fbeltrao/azureiot-telemetrysimulator:latest fbeltrao/azureiot-telemetrysimulator:1.0
docker push fbeltrao/azureiot-telemetrysimulator:latest && docker push fbeltrao/azureiot-telemetrysimulator:1.0

docker build -f src\IotSimulatorDeviceProvisioning\Dockerfile -t fbeltrao/azureiot-simulatordeviceprovisioning:latest  .
docker tag fbeltrao/azureiot-simulatordeviceprovisioning:latest fbeltrao/azureiot-simulatordeviceprovisioning:1.0
docker push fbeltrao/azureiot-simulatordeviceprovisioning:latest && docker push fbeltrao/azureiot-simulatordeviceprovisioning:1.0