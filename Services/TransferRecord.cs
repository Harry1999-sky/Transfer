namespace LanTransfer.Services;

public enum TransferDirection { Upload, Download }

public record TransferRecord(
    string FileName,
    long FileSize,
    TransferDirection Direction,
    DateTime Time,
    string RemoteIp
);
