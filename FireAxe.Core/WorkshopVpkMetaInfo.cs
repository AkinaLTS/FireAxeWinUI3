using System;

namespace FireAxe;

public class WorkshopVpkMetaInfo
{
    public required ulong PublishedFileId { get; init; }

    public required ulong TimeUpdated { get; init; }

    public required string CurrentFile 
    { 
        get; 
        init
        {
            if (!FileSystemUtils.IsSafeSingleFileName(value))
            {
                throw new ArgumentException("CurrentFile must be a single file name without directory segments.", nameof(value));
            }
            field = value;
        }
    }
}
