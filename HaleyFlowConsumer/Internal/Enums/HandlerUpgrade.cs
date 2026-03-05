namespace Haley.Enums {
    /// <summary>
    /// Controls whether a workflow instance can upgrade to a newer handler version.
    /// </summary>
    public enum HandlerUpgrade : byte {
        /// <summary>Stick to the handler version pinned on first event for this entity.</summary>
        Pinned = 1,
        /// <summary>Allow upgrading to the latest registered handler version.</summary>
        AllowUpgrade = 2
    }
}
