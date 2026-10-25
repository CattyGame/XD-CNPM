namespace AURA.Analysis.Api.Upload;

public interface IPrivateStorageReadiness
{ Task<bool> CheckPrivateContainersAsync(CancellationToken ct); }
