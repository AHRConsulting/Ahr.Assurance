namespace Ahr.Assurance.Execution;

/// <summary>When an attachment is kept in the evidence.</summary>
public enum AttachmentKeep
{
    /// <summary>Kept on every run.</summary>
    Always,

    /// <summary>Kept only when its scenario doesn't pass: a check fails, a step errors, or the run stops before the scenario ends.
    /// Content isn't written until then, so a passing run leaves nothing behind.</summary>
    OnFailure,
}
