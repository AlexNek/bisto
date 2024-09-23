namespace Bisto;

public class StorageOptions
{
    public int CacheSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the deleted items entries per block.
    /// </summary>
    /// <value>The entries per block.</value>
    public int EntriesPerBlock { get; set; } = 256;
    
    public bool UseRoundedBlockSize { get; set; } = true;

    public int StreamInactivityTimeout { get; set; } = 5;// in minutes

    /// <summary>
    /// Gets or sets a value indicating whether use journaling.
    /// For many operations per second, it is better to disable journaling.
    /// </summary>
    /// <value><c>true</c> use journaling; otherwise, <c>false</c>.</value>
    public bool UseJournaling { get; set; } = true;
}
