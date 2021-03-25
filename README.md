# RfmOta

A library for the flashing hex files to Rfm69 nodes over the wireless.

## Using OtaService

``` csharp
try
{
    using var stream = File.OpenRead(options.HexFile);
    using var otaservice = _serviceProvider.GetService<IOtaService>();

    if (otaservice.OtaUpdate(options.SerialPort, options.BaudRate, (byte) options.OutputPower, stream, out uint crc))
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
