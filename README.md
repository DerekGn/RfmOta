# RfmOta

![GitHub Actions](https://github.com/DerekGn/RfmOta/actions/workflows/build.yml/badge.svg)

[![NuGet](https://img.shields.io/nuget/v/RfmOta.svg?style=flat-square)](https://www.nuget.org/packages/RfmOta/)

A library for the flashing hex files to Rfm69 nodes over the air.

## Installing RfmOta

Install the RfmOta package via nuget package manager console:

```
Install-Package RfmOta
```

## Supported .Net Runtimes

The RfmOta package is compatible with the following runtimes:

* .NET Core 8

## Using OtaService

``` csharp
try
{
    // The DI provider must be configured to instantiate the 
    // dependencies ILogger<IOtaService>, IRfmUsb, IIntelHexStreamReaderFactory
    using var stream = File.OpenRead(options.HexFile);
    using var otaservice = _serviceProvider.GetService<IOtaService>();

    // RfmUsb instance should be configured as singleton
    using var rfmUsb = _serviceProvider.GetService<IRfmUsb>();

    // Open the rfmUsb instance and configure the port and baud rate
    rfmUsb.Open(options.SerialPort, options.BaudRate);

    if (otaservice.OtaUpdate((byte) options.OutputPower, stream, out uint crc))
    {
        logger.LogInformation($"OTA flash update completed. Crc: [0x{crc:X}]");
    }
    else
    {
        logger.LogWarning("OTA flash update failed");
    }
}
catch(RfmUsbSerialPortNotFoundException ex)
{
    logger.LogError(ex.Message);
}
catch (RfmUsbCommandExecutionException ex)
{
    logger.LogError(ex.Message);
}
catch (FileNotFoundException)
{
    logger.LogError($"Unable to find file: [{options.HexFile}]");
}
catch (Exception ex)
{
    logger.LogError(ex, "An unhandled exception occurred");
}
```
