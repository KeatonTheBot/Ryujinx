using Ryujinx.Memory;
using System.Runtime.Versioning;

namespace Ryujinx.Cpu.AppleHv
{
    [SupportedOSPlatform("macos")]
    class HvMemoryBlockAllocator(HvIpaAllocator ipaAllocator, ulong blockAlignment)
        : PrivateMemoryAllocatorImpl<HvMemoryBlockAllocator.Block>(blockAlignment, MemoryAllocationFlags.None)
    {
        public class Block : PrivateMemoryAllocator.Block
        {
            private readonly HvIpaAllocator _ipaAllocator;
            public ulong Ipa { get; }

            public Block(HvIpaAllocator ipaAllocator, MemoryBlock memory, ulong size) : base(memory, size)
            {
                _ipaAllocator = ipaAllocator;

                lock (ipaAllocator)
                {
                    Ipa = ipaAllocator.Allocate(size);
                }

                HvApi.hv_vm_map((ulong)Memory.Pointer, Ipa, size, HvMemoryFlags.Read | HvMemoryFlags.Write).ThrowOnError();
            }

            public override void Destroy()
            {
                HvApi.hv_vm_unmap(Ipa, Size).ThrowOnError();

                lock (_ipaAllocator)
                {
                    _ipaAllocator.Free(Ipa, Size);
                }

                base.Destroy();
            }
        }

        public HvMemoryBlockAllocation Allocate(ulong size, ulong alignment)
        {
            var allocation = Allocate(size, alignment, CreateBlock);

            return new HvMemoryBlockAllocation(this, allocation.Block, allocation.Offset, allocation.Size);
        }

        private Block CreateBlock(MemoryBlock memory, ulong size)
        {
            return new Block(ipaAllocator, memory, size);
        }
    }
}
