Set-StrictMode -Version 2.0
if (-not ('Rm032.Canonical' -as [type])) {
Add-Type -ReferencedAssemblies 'System.dll','System.Core.dll','System.Xml.dll' -TypeDefinition @'
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
namespace Rm032 {
 public static class Canonical {
  sealed class Number { public string Value; public Number(string v){Value=v;} }
  sealed class Parser {
   string s; int p; int depth;
   static readonly System.Text.RegularExpressions.Regex numeric = new System.Text.RegularExpressions.Regex(@"\G-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
   public Parser(string value){s=value;}
   void White(){while(p<s.Length && " \r\n\t".IndexOf(s[p])>=0)p++;}
   void Fail(){throw new FormatException("STRICT_JSON_ERROR at "+p);}
   string Str(){
    if(p>=s.Length||s[p++]!='"')Fail(); var b=new StringBuilder();
    while(p<s.Length){char c=s[p++]; if(c=='"')return b.ToString(); if(c<32)Fail();
     if(c!='\\'){b.Append(c);continue;} if(p>=s.Length)Fail(); c=s[p++];
     switch(c){case '"':case '\\':case '/':b.Append(c);break;case 'b':b.Append('\b');break;case 'f':b.Append('\f');break;
      case 'n':b.Append('\n');break;case 'r':b.Append('\r');break;case 't':b.Append('\t');break;
      case 'u': if(p+4>s.Length)Fail(); int n; if(!Int32.TryParse(s.Substring(p,4),System.Globalization.NumberStyles.HexNumber,null,out n))Fail();b.Append((char)n);p+=4;break;
      default:Fail();break;}
    } Fail();return null;
   }
   object Value(){White();if(++depth>100)Fail();object v; if(p>=s.Length){Fail();return null;} char c=s[p];
    if(c=='{'){p++;var d=new Dictionary<string,object>(StringComparer.Ordinal);White();if(p<s.Length&&s[p]=='}')p++;
     else while(true){White();string k=Str();White();if(p>=s.Length||s[p++]!=':')Fail(); if(d.ContainsKey(k))throw new FormatException("DUPLICATE_JSON_KEY: "+k);d[k]=Value();White();if(p>=s.Length)Fail();c=s[p++];if(c=='}')break;if(c!=',')Fail();}v=d;}
    else if(c=='['){p++;var a=new List<object>();White();if(p<s.Length&&s[p]==']')p++;else while(true){a.Add(Value());White();if(p>=s.Length)Fail();c=s[p++];if(c==']')break;if(c!=',')Fail();}v=a;}
    else if(c=='"')v=Str();
    else if(p+4<=s.Length&&String.CompareOrdinal(s,p,"true",0,4)==0){p+=4;v=true;}
    else if(p+5<=s.Length&&String.CompareOrdinal(s,p,"false",0,5)==0){p+=5;v=false;}
    else if(p+4<=s.Length&&String.CompareOrdinal(s,p,"null",0,4)==0){p+=4;v=null;}
    else {var m=numeric.Match(s,p);if(!m.Success||m.Index!=p)Fail();p+=m.Length;v=new Number(m.Value);}
    depth--;return v;
   }
   public object Parse(){var v=Value();White();if(p!=s.Length)Fail();return v;}
  }
  static string Quote(string s){var b=new StringBuilder("\"");foreach(char c in s){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;default:if(c<32)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}return b.Append('"').ToString();}
  static string Emit(object v){if(v==null)return "null";if(v is string)return Quote((string)v);if(v is bool)return (bool)v?"true":"false";if(v is Number)return ((Number)v).Value;
   var d=v as Dictionary<string,object>;if(d!=null)return "{"+String.Join(",",d.Keys.OrderBy(k=>k,StringComparer.Ordinal).Select(k=>Quote(k)+":"+Emit(d[k])).ToArray())+"}";
   return "["+String.Join(",",((List<object>)v).Select(Emit).ToArray())+"]";
  }
  public static string Json(string json){return Emit(new Parser(json).Parse());}
  public static void ValidateJson(string json){new Parser(json).Parse();}
  static void Token(StringBuilder b,string t,string v){b.Append(t).Append(Encoding.UTF8.GetByteCount(v)).Append(':').Append(v);}
  static void Node(XmlNode n,StringBuilder b,bool preserve){
   if(n.NodeType==XmlNodeType.Comment||n.NodeType==XmlNodeType.ProcessingInstruction)return;
   if(n.NodeType==XmlNodeType.Element){var e=(XmlElement)n;string sp=e.GetAttribute("space","http://www.w3.org/XML/1998/namespace");if(sp=="preserve")preserve=true;else if(sp=="default")preserve=false;
    Token(b,"E",e.NamespaceURI+"|"+e.LocalName);
    foreach(XmlAttribute a in e.Attributes.Cast<XmlAttribute>().Where(a=>a.NamespaceURI!="http://www.w3.org/2000/xmlns/").OrderBy(a=>a.NamespaceURI+"|"+a.LocalName,StringComparer.Ordinal)){Token(b,"A",a.NamespaceURI+"|"+a.LocalName);Token(b,"V",a.Value);}
    b.Append('>');bool mixed=e.ChildNodes.Cast<XmlNode>().Any(c=>(c.NodeType==XmlNodeType.Text||c.NodeType==XmlNodeType.CDATA)&&!String.IsNullOrWhiteSpace(c.Value));
    bool child=e.ChildNodes.Cast<XmlNode>().Any(c=>c.NodeType==XmlNodeType.Element);
    foreach(XmlNode c in e.ChildNodes){if(!preserve&&!mixed&&child&&(c.NodeType==XmlNodeType.Whitespace||c.NodeType==XmlNodeType.SignificantWhitespace||c.NodeType==XmlNodeType.Text)&&String.IsNullOrWhiteSpace(c.Value))continue;Node(c,b,preserve);}b.Append('/');
   } else if(n.NodeType==XmlNodeType.Text||n.NodeType==XmlNodeType.CDATA||n.NodeType==XmlNodeType.Whitespace||n.NodeType==XmlNodeType.SignificantWhitespace)Token(b,"T",n.Value);
  }
  public static string Xml(string xml,string mode){var s=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=134217728};var d=new XmlDocument{XmlResolver=null,PreserveWhitespace=true};using(var r=XmlReader.Create(new System.IO.StringReader(xml),s))d.Load(r);
   var root=d.DocumentElement;foreach(XmlNode n in root.ChildNodes.Cast<XmlNode>().ToArray()){var e=n as XmlElement;if(e==null)continue;bool runtime=e.NamespaceURI==""&&(e.LocalName=="playcount"||e.LocalName=="playtime"||e.LocalName=="lastplayed");bool locator=e.NamespaceURI==""&&e.LocalName=="path";
    if((mode=="content"&&(runtime||locator))||(mode=="runtime"&&!runtime))root.RemoveChild(n);}
   var b=new StringBuilder("rm-xml-entry-v1|");Node(root,b,false);return b.ToString();
  }
 }
}
'@
}
function Get-RmTextDigest {param([string]$Text) $sha=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))).Replace('-','').ToLowerInvariant())}finally{$sha.Dispose()}}
function Get-RmDigest {param($Value) return Get-RmTextDigest ([Rm032.Canonical]::Json((ConvertTo-Json -InputObject $Value -Depth 80 -Compress)))}
function Get-RmByteDigest {param([string]$Path) if(!(Test-RmFileExists $Path)){return 'absent'};$s=Open-RmFile $Path ([IO.FileMode]::Open) ([IO.FileAccess]::Read) ([IO.FileShare]::Read);$h=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($h.ComputeHash($s)).Replace('-','').ToLowerInvariant())}finally{$h.Dispose();$s.Dispose()}}
function Read-RmStrictJson {param([string]$Path) $text=Read-RmAllText $Path ([Text.Encoding]::UTF8);[Rm032.Canonical]::ValidateJson($text);return ConvertFrom-Json -InputObject $text}
function Sort-RmOrdinal {
 param($Values,[string]$Property)
 $map=New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([StringComparer]::Ordinal)
 foreach($v in $Values){$key=[string]$v;if($Property){$key=[string](Get-RmProperty $v $Property)};if(!$map.ContainsKey($key)){$map[$key]=New-Object 'System.Collections.Generic.List[object]'};$map[$key].Add($v)}
 foreach($k in $map.Keys){foreach($v in $map[$k]){Write-Output -NoEnumerate $v}}
}
function Get-RmProperty {param($Object,[string]$Name,$Default=$null) if($null -eq $Object){return $Default};$p=$Object.PSObject.Properties[$Name];if($p){return $p.Value};return $Default}
function Set-RmProperty {param($Object,[string]$Name,$Value) $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value -Force}
function Get-RmNormalizedLocator {param([string]$Value) if(!$Value){return $null};return $Value.Replace('\','/').Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()}
function Assert-RmFields {param($Object,[string[]]$Allowed,[string[]]$Required=@()) if($null -eq $Object){throw 'SCHEMA_OBJECT_REQUIRED'};foreach($p in $Object.PSObject.Properties){if($p.Name -notin $Allowed){throw ('SCHEMA_UNKNOWN_FIELD: '+$p.Name)}};foreach($n in $Required){if(!$Object.PSObject.Properties[$n]){throw ('SCHEMA_REQUIRED_FIELD: '+$n)}}}
function Assert-RmUuid {param([string]$Value) $id=[Guid]::Empty;if(![Guid]::TryParseExact($Value,'D',[ref]$id)-or$id -eq [Guid]::Empty){throw 'SCHEMA_UUID_REQUIRED'}}
Export-ModuleMember -Function *-Rm*
