tag="1.1"
if [ "$1" != "" ]; then
    tag="$1"
fi

docker build -f src/IotTelemetrySimulator/Dockerfile -t fbeltrao/azureiot-telemetrysimulator:latest .
docker tag fbeltrao/azureiot-telemetrysimulator:latest fbeltrao/azureiot-telemetrysimulator:$tag
docker push fbeltrao/azureiot-telemetrysimulator:latest && docker push fbeltrao/azureiot-telemetrysimulator:$tag

docker build -f src/IotSimulatorDeviceProvisioning/Dockerfile -t fbeltrao/azureiot-simulatordeviceprovisioning:latest .
docker tag fbeltrao/azureiot-simulatordeviceprovisioning:latest fbeltrao/azureiot-simulatordeviceprovisioning:$tag
docker push fbeltrao/azureiot-simulatordeviceprovisioning:latest && docker push fbeltrao/azureiot-simulatordeviceprovisioning:$tag
