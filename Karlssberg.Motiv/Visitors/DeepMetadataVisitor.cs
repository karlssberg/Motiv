﻿using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Visitors;

public class DeepMetadataVisitor<TMetadata> : DefaultMetadataVisitor<TMetadata>
{
    public override IEnumerable<TMetadata> Visit(IChangeMetadataBooleanResult<TMetadata> changeMetadataBooleanResult) =>
        changeMetadataBooleanResult.Causes
            .SelectMany(Visit)
            .IfEmptyThen(changeMetadataBooleanResult.Metadata);
    
    public override IEnumerable<TMetadata> Visit(IChangeHigherOrderMetadataBooleanResult<TMetadata> changeMetadataBooleanResult) =>
        changeMetadataBooleanResult.Causes
            .SelectMany(Visit)
            .IfEmptyThen(changeMetadataBooleanResult.Metadata);
}