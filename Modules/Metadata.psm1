Set-StrictMode -Version 2.0
function New-RmXmlSettings {
    $s=New-Object Xml.XmlReaderSettings
    $s.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$s.XmlResolver=$null
    $s.MaxCharactersInDocument=134217728;$s.CheckCharacters=$true
    return $s
}
function Read-RmEsDeXml {
    param([string]$Path)
    $reader=$null;$stream=$null;$strictError=$null
    try {
        $stream=Open-RmFile $Path ([IO.FileMode]::Open) ([IO.FileAccess]::Read) ([IO.FileShare]::Read)
        $reader=[Xml.XmlReader]::Create($stream,(New-RmXmlSettings))
        $doc=New-Object Xml.XmlDocument;$doc.XmlResolver=$null;$doc.PreserveWhitespace=$true;$doc.Load($reader)
        if ($doc.DocumentElement.LocalName -cne 'gameList' -or $doc.DocumentElement.NamespaceURI -ne '') {throw 'Expected unnamespaced gameList root element.'}
        return [pscustomobject]@{root=$doc.DocumentElement;parseMode='strict';strictError=$null;systemMetadataXml=@();topLevelNames=@('gameList')}
    } catch {$strictError=$_.Exception.Message}
    finally {if ($reader) {$reader.Dispose()};if ($stream) {$stream.Dispose()}}
    # Exact ES-DE dialect only. No source edits and no malformed XML recovery.
    $info=Get-RmFileStat $Path
    if ($info.Length -gt 134217728) {throw ('XML source too large for targeted fallback. Strict failure: '+$strictError)}
    $bytes=Read-RmAllBytes $Path;$offset=0;$sourceEncoding='UTF-8'
    $encoding=New-Object Text.UTF8Encoding($false,$true)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) {$offset=3}
    elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 255 -and $bytes[1] -eq 254) {$offset=2;$sourceEncoding='UTF-16';$encoding=New-Object Text.UnicodeEncoding($false,$true,$true)}
    elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 254 -and $bytes[1] -eq 255) {$offset=2;$sourceEncoding='UTF-16';$encoding=New-Object Text.UnicodeEncoding($true,$true,$true)}
    $text=$encoding.GetString($bytes,$offset,($bytes.Length-$offset))
    $declaration=[regex]::Match($text,'\A\s*<\?xml\s[^?]*\?>')
    if ($declaration.Success) {
        $dr=[Xml.XmlReader]::Create((New-Object IO.StringReader($declaration.Value+'<probe/>')),(New-RmXmlSettings))
        try {while ($dr.Read()) {}} finally {$dr.Dispose()}
        if ($declaration.Value -match '(?i)encoding\s*=\s*["'']([^"'']+)["'']') {
            $declared=$Matches[1].ToUpperInvariant().Replace('UTF8','UTF-8')
            if ($declared -ne $sourceEncoding) {throw ('Unsupported/mismatched encoding in ES-DE envelope fallback. Strict failure: '+$strictError)}
        }
        $text=$text.Substring($declaration.Length)
    }
    $fr=$null
    try {
        $fr=[Xml.XmlReader]::Create((New-Object IO.StringReader('<esDeEnvelope>'+$text+'</esDeEnvelope>')),(New-RmXmlSettings))
        $wrapped=New-Object Xml.XmlDocument;$wrapped.XmlResolver=$null;$wrapped.PreserveWhitespace=$true;$wrapped.Load($fr)
        $elements=@($wrapped.DocumentElement.ChildNodes | Where-Object {$_.NodeType -eq [Xml.XmlNodeType]::Element})
        if ($elements.Count -ne 2 -or @($elements | Where-Object {$_.Name -ceq 'gameList' -and $_.NamespaceURI -eq ''}).Count -ne 1 -or
            @($elements | Where-Object {$_.Name -ceq 'alternativeEmulator' -and $_.NamespaceURI -eq ''}).Count -ne 1) {throw 'Fallback permits exactly gameList and alternativeEmulator at document level.'}
        foreach ($node in $wrapped.DocumentElement.ChildNodes) {
            if ($node.NodeType -notin @([Xml.XmlNodeType]::Element,[Xml.XmlNodeType]::Whitespace,[Xml.XmlNodeType]::SignificantWhitespace,[Xml.XmlNodeType]::Comment)) {throw 'Unsupported top-level node in ES-DE envelope.'}
        }
        $alt=@($elements | Where-Object {$_.Name -ceq 'alternativeEmulator'})[0]
        $children=@($alt.ChildNodes | Where-Object {$_.NodeType -eq [Xml.XmlNodeType]::Element})
        foreach ($node in $alt.ChildNodes) {
            if ($node.NodeType -notin @([Xml.XmlNodeType]::Element,[Xml.XmlNodeType]::Whitespace,[Xml.XmlNodeType]::SignificantWhitespace,[Xml.XmlNodeType]::Comment)) {throw 'Unsupported alternativeEmulator child node.'}
        }
        if ($alt.Attributes.Count -ne 0 -or $children.Count -ne 1 -or $children[0].Name -cne 'label' -or
            $children[0].NamespaceURI -ne '' -or $children[0].Attributes.Count -ne 0 -or
            @($children[0].ChildNodes | Where-Object {$_.NodeType -eq [Xml.XmlNodeType]::Element}).Count -gt 0) {throw 'Unsupported alternativeEmulator shape.'}
        return [pscustomobject]@{root=@($elements | Where-Object {$_.Name -ceq 'gameList'})[0];parseMode='es-de-envelope';
            strictError=$strictError;systemMetadataXml=@($alt.OuterXml);topLevelNames=@($elements | ForEach-Object {$_.Name})}
    } catch {throw ('Strict XML failure: '+$strictError+' | Targeted ES-DE fallback refused: '+$_.Exception.Message)}
    finally {if ($fr) {$fr.Dispose()}}
}
function Get-RmMetadata {
    param($Context,$Inventory)
    $entries=New-Object 'System.Collections.Generic.List[object]';$sources=New-Object 'System.Collections.Generic.List[object]'
    foreach ($f in $Inventory.files) {
        if ($f.relativePath -notmatch '^MEDIAS/([^/]+)/metadata/es-de-gamelist\.xml$') {continue}
        $console=$Matches[1]
        $source=[pscustomobject]@{relativePath=$f.relativePath;sizeBytes=$f.sizeBytes;modifiedAtUtc=$f.modifiedAtUtc;
            status='loaded';entryCount=0;gameEntryCount=0;folderEntryCount=0;folderMetadata=@();parseMode=$null;strictError=$null;systemMetadataXml=@();topLevelNames=@()}
        $sources.Add($source)
        try {
            $xmlPath=$Context.Root+'\'+$f.relativePath.Replace('/','\')
            $bytesBefore=Get-RmByteDigest $xmlPath
            $parsed=Read-RmEsDeXml $xmlPath
            if((Get-RmByteDigest $xmlPath)-ne$bytesBefore){throw 'XML_CHANGED_DURING_SCAN'}
            Set-RmProperty $source 'parsedByteSha256' $bytesBefore
            foreach ($node in $parsed.root.ChildNodes) {
                if ($node.NodeType -eq [Xml.XmlNodeType]::Element -and $node.LocalName -in @('game','folder') -and $node.NamespaceURI -ne '') {throw 'Unsupported namespace on game/folder entry.'}
            }
            $source.parseMode=$parsed.parseMode;$source.strictError=$parsed.strictError
            $source.systemMetadataXml=$parsed.systemMetadataXml;$source.topLevelNames=$parsed.topLevelNames
            if ($parsed.parseMode -eq 'es-de-envelope') {Add-RmAnomaly $Context 'ESDE_ENVELOPE_USED' $f.relativePath 'Strict parsing failed; exact ES-DE two-element envelope accepted in memory.' 'Info' $console}
            $index=0
            foreach ($node in $parsed.root.SelectNodes('game|folder')) {
                $raw=[ordered]@{}
                foreach ($child in $node.ChildNodes) {
                    if ($child.NodeType -ne [Xml.XmlNodeType]::Element) {continue}
                    if (!$raw.Contains($child.LocalName)) {$raw[$child.LocalName]=$child.InnerText}
                    else {$raw[$child.LocalName]=@($raw[$child.LocalName])+@($child.InnerText)}
                }
                $resolved=$null
                try {
                    if (!$raw.Contains('path') -or @($raw['path']).Count -ne 1) {throw 'Missing or repeated game path.'}
                    $resolved=Resolve-RmRelativePath ('ROMS/'+$console) $raw['path'] ('ROMS/'+$console)
                } catch {Add-RmAnomaly $Context 'METADATA_PATH_INVALID' $f.relativePath $_.Exception.Message 'Warning' $console}
                $entry=[pscustomobject]@{console=$console;sourcePath=$f.relativePath;entryIndex=$index;entryKind=$node.LocalName;
                    resolvedPath=$resolved;raw=[pscustomobject]$raw;rawEntryXml=$node.OuterXml}
                if ($node.LocalName -eq 'folder') {$source.folderEntryCount++;$source.folderMetadata+=@($entry)}
                else {$source.gameEntryCount++;$entries.Add($entry)}
                $index++
            }
            $source.entryCount=$index
        } catch {$source.status='error';Add-RmAnomaly $Context 'XML_PARSE_ERROR' $f.relativePath $_.Exception.Message 'Error' $console}
    }
    foreach ($console in @($Inventory.directories | Where-Object {$_ -match '^ROMS/[^/]+$'} | ForEach-Object {$_.Split('/')[1]})) {
        if (@($sources | Where-Object {$_.relativePath -eq ('MEDIAS/'+$console+'/metadata/es-de-gamelist.xml')}).Count -eq 0) {Add-RmAnomaly $Context 'METADATA_SOURCE_MISSING' ('MEDIAS/'+$console+'/metadata/es-de-gamelist.xml') 'No ES-DE source found.' 'Warning' $console}
    }
    return [pscustomobject]@{entries=$entries.ToArray();sources=$sources.ToArray()}
}
Export-ModuleMember -Function Get-RmMetadata
