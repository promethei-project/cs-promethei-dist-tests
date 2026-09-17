using Utils;

namespace PrometheiClient
{
    public class PurchaseParams
    {
        private readonly ByteSize blockSize = 512.KB();

        public PurchaseParams(
            int nodes,
            int tolerance,
            TimeSpan duration,
            TimeSpan expiry,
            ByteSize uploadFilesize,
            TestToken pricePerByteSecond,
            TestToken collateralPerByte,
            int proofProbability
        )
        {
            Nodes = nodes;
            Tolerance = tolerance;
            Duration = duration;
            Expiry = expiry;
            UploadFilesize = uploadFilesize;
            PricePerByteSecond = pricePerByteSecond;
            CollateralPerByte = collateralPerByte;
            ProofProbability = proofProbability;

            EncodedDatasetSize = CalculateEncodedDatasetSize();
            SlotSize = CalculateSlotSize();
            CollateralRequiredPerSlot = CalculateCollateralPerSlot();
            PaymentPerSlot = CalculatePaymentPerSlot();
        }

        public static PurchaseParams Default
        {
            get; private set;
        }

        static PurchaseParams()
        {
            Default = new PurchaseParams(
                nodes: 4,
                tolerance: 2,
                duration: TimeSpan.FromMinutes(20.0),
                expiry: TimeSpan.FromMinutes(10.0),
                uploadFilesize: 32.MB(),
                pricePerByteSecond: 1000.TstWei(),
                collateralPerByte: 1.TstWei(),
                proofProbability: 20
            );
        }

        public int Nodes { get; }
        public int Tolerance { get; }
        public TimeSpan Duration { get; }
        public TimeSpan Expiry { get; }
        public ByteSize UploadFilesize { get; }
        public TestToken PricePerByteSecond { get; }
        public TestToken CollateralPerByte { get; }
        public ByteSize EncodedDatasetSize { get; }
        public ByteSize SlotSize { get; }
        public TestToken CollateralRequiredPerSlot { get; }
        public TestToken PaymentPerSlot { get; }
        public int ProofProbability { get; }

        public PurchaseParams WithNodes(int value)
        {
            return new PurchaseParams(value, Tolerance, Duration, Expiry, UploadFilesize, PricePerByteSecond, CollateralPerByte, ProofProbability);
        }

        public PurchaseParams WithTolerance(int value)
        {
            return new PurchaseParams(Nodes, value, Duration, Expiry, UploadFilesize, PricePerByteSecond, CollateralPerByte, ProofProbability);
        }

        public PurchaseParams WithDuration(TimeSpan value)
        {
            return new PurchaseParams(Nodes, Tolerance, value, Expiry, UploadFilesize, PricePerByteSecond, CollateralPerByte, ProofProbability);
        }

        public PurchaseParams WithExpiry(TimeSpan value)
        {
            return new PurchaseParams(Nodes, Tolerance, Duration, value, UploadFilesize, PricePerByteSecond, CollateralPerByte, ProofProbability);
        }

        public PurchaseParams WithUploadFilesize(ByteSize value)
        {
            return new PurchaseParams(Nodes, Tolerance, Duration, Expiry, value, PricePerByteSecond, CollateralPerByte, ProofProbability);
        }

        public PurchaseParams WithPricePerByteSecond(TestToken value)
        {
            return new PurchaseParams(Nodes, Tolerance, Duration, Expiry, UploadFilesize, value, CollateralPerByte, ProofProbability);
        }

        public PurchaseParams WithCollateralPerByte(TestToken value)
        {
            return new PurchaseParams(Nodes, Tolerance, Duration, Expiry, UploadFilesize, PricePerByteSecond, value, ProofProbability);
        }

        public PurchaseParams WithProofProbability(int value)
        {
            return new PurchaseParams(Nodes, Tolerance, Duration, Expiry, UploadFilesize, PricePerByteSecond, CollateralPerByte, value);
        }

        public override string ToString()
        {
            return "(" +
                $"Nodes: {Nodes}, " +
                $"Tolerance: {Tolerance}, " +
                $"Duration: {Time.FormatDuration(Duration)}, " +
                $"Expiry: {Time.FormatDuration(Expiry)}, " +
                $"PricePerByteSecond: {PricePerByteSecond}, " +
                $"ProofProbability: {ProofProbability}, " +
                $"CollateralPerByte: {CollateralPerByte}, " +
                $"EncodedDatasetSize: {EncodedDatasetSize}, " +
                $"SlotSize: {SlotSize}, " +
                $"CollateralRequiredPerSlot: {CollateralRequiredPerSlot}, " +
                $"PaymentPerSlot: {PaymentPerSlot})";
        }

        private ByteSize CalculateSlotSize()
        {
            // encoded dataset is divided over the nodes.
            // then each slot is rounded up to the nearest power-of-two blocks.
            var numBlocks = EncodedDatasetSize.DivUp(blockSize);
            var numSlotBlocks = Int.DivUp(numBlocks, Nodes);

            // Next power of two:
            var numSlotBlocksPow2 = IsOrNextPowerOf2(numSlotBlocks);
            return new ByteSize(blockSize.SizeInBytes * numSlotBlocksPow2);
        }

        private ByteSize CalculateEncodedDatasetSize()
        {
            var numBlocks = UploadFilesize.DivUp(blockSize);

            var ecK = Nodes - Tolerance;
            var ecM = Tolerance;

            // for each K blocks, we generate M parity blocks
            var numParityBlocks = Int.DivUp(numBlocks, ecK) * ecM;
            var totalBlocks = numBlocks + numParityBlocks;

            return new ByteSize(blockSize.SizeInBytes * totalBlocks);
        }

        private TestToken CalculateCollateralPerSlot()
        {
            return CollateralPerByte * SlotSize.SizeInBytes;
        }

        private TestToken CalculatePaymentPerSlot()
        {
            return PricePerByteSecond * SlotSize.SizeInBytes * Convert.ToInt64(Duration.TotalSeconds);
        }

        private int IsOrNextPowerOf2(int n)
        {
            if (IsPowerOfTwo(n)) return n;
            var result = 2;
            while (result < n)
            {
                result = result * 2;
            }
            return result;
        }

        private static bool IsPowerOfTwo(ByteSize size)
        {
            var x = size.SizeInBytes;
            return (x != 0) && ((x & (x - 1)) == 0);
        }

        private static bool IsPowerOfTwo(int x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }
    }
}
