namespace Motiv.Serialization;

/// <summary>Stable machine-readable codes for rule-document errors.</summary>
public enum RuleErrorCode
{
    /// <summary>The document or a node within it is structurally invalid.</summary>
    InvalidNode,

    /// <summary>The document references a spec name that is not registered.</summary>
    UnknownSpec,

    /// <summary>A referenced spec's model type does not match the model type of the load.</summary>
    ModelTypeMismatch,

    /// <summary>A node's metadata cannot be reconciled with the metadata type of the load.</summary>
    MetadataTypeMismatch,

    /// <summary>A node's whenTrue and whenFalse payloads are of different kinds.</summary>
    MixedWhenTrueFalseKinds,

    /// <summary>The document uses expression nodes but expression support is not enabled.</summary>
    ExpressionsNotEnabled,

    /// <summary>A synchronous load referenced an asynchronous registry entry.</summary>
    AsyncSpecInSyncLoad,

    /// <summary>The document exceeds the configured depth or node-count limits.</summary>
    DocumentTooLarge,

    /// <summary>A required parameter was not supplied and has no default.</summary>
    MissingParameter,

    /// <summary>A supplied parameter is not declared by the document.</summary>
    SurplusParameter,

    /// <summary>A supplied parameter value does not match the declared parameter type.</summary>
    ParameterTypeMismatch,

    /// <summary>A payload string or 'n' slot references a parameter that is not declared.</summary>
    UnknownParameterReference,

    /// <summary>A higher-order node references a collection path that is not registered.</summary>
    UnknownCollection,

    /// <summary>An async spec was referenced inside a higher-order subtree, which must be fully synchronous.</summary>
    AsyncSpecInHigherOrder
}
