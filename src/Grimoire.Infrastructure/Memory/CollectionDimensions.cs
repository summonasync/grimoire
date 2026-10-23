using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

public static class CollectionDimensions
{
    public static int? From(CollectionInfo info)
    {
        var config = info.Config?.Params?.VectorsConfig;

        if (config is null)
        {
            return null;
        }

        return config.ConfigCase switch
        {
            VectorsConfig.ConfigOneofCase.Params => (int?)config.Params?.Size,
            VectorsConfig.ConfigOneofCase.ParamsMap => (int?)config.ParamsMap?.Map.Values
                .Select(p => (ulong?)p.Size)
                .FirstOrDefault(),
            _ => null,
        };
    }
}
