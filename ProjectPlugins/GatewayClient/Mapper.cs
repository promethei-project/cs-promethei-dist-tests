using PrometheiClient;

namespace GatewayClient
{
    // This one's a little weird:
    // Map the gatewayAPI types to the prometheiAPI types.
    // They should match 100% because the gateway simply forwards.
    // After that, we use the PrometheiClient mapper to map to non-volatile types.
    public class Mapper
    {
        private readonly PrometheiClient.Mapper submapper = new PrometheiClient.Mapper();

        public LocalDataset Map(GatewayApi.DataItem dataItem)
        {
            return submapper.Map(new PrometheiOpenApi.DataItem
            {
                Cid = dataItem.Cid,
                Manifest = Map(dataItem.Manifest),
                AdditionalProperties = dataItem.AdditionalProperties
            });
        }

        private PrometheiOpenApi.ManifestItem Map(GatewayApi.ManifestItem manifest)
        {
            return new PrometheiOpenApi.ManifestItem
            {
                BlockSize = manifest.BlockSize,
                DatasetSize = manifest.DatasetSize,
                Filename = manifest.Filename,
                Mimetype = manifest.Mimetype,
                Protected = manifest.Protected,
                TreeCid = manifest.TreeCid,
                AdditionalProperties = manifest.AdditionalProperties
            };
        }
    }
}
