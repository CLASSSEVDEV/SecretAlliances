using SecretAlliances;
using System.Collections.Generic;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

public class SecretAlliancesSaveDefiner : SaveableTypeDefiner
{
    public SecretAlliancesSaveDefiner() : base(2340000) { }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(SecretAllianceRecord), 1);
        AddClassDefinition(typeof(AllianceIntelligence), 2);
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<SecretAllianceRecord>));
        ConstructContainerDefinition(typeof(List<AllianceIntelligence>));
        ConstructContainerDefinition(typeof(List<MBGUID>));
    }
}
