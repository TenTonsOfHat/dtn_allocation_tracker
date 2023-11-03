namespace Library;

#pragma warning disable 108, 114, 472, 612, 1573, 1591, 8073, 3016, 8603
public partial class LocationV2
{
    public ICollection<TerminalV2> MappedTerminals { get; set; }

    public ICollection<TerminalV2> AllTerminals
    {
        get
        {
            if (Terminal != null)
                return new[] { Terminal };

            if (MappedTerminals != null) return MappedTerminals;

            return Array.Empty<TerminalV2>();
        }
    }
}