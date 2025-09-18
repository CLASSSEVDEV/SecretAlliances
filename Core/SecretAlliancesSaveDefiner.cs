using SecretAlliances.Core;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace SecretAlliances.Core
{
    public class SecretAlliancesSaveDefiner : SaveableTypeDefiner
    {
        public SecretAlliancesSaveDefiner() : base(2340000) { }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(SecretAllianceRecord), 1);
        AddClassDefinition(typeof(AllianceIntelligence), 2);
        AddClassDefinition(typeof(AllianceContract), 3);
        AddClassDefinition(typeof(MilitaryCoordinationData), 4);
        AddClassDefinition(typeof(EconomicNetworkData), 5);
        AddClassDefinition(typeof(SpyNetworkData), 6);
        AddClassDefinition(typeof(TradeTransferRecord), 7);
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<SecretAllianceRecord>));
        ConstructContainerDefinition(typeof(List<AllianceIntelligence>));
        ConstructContainerDefinition(typeof(List<MBGUID>));
        ConstructContainerDefinition(typeof(List<AllianceContract>));
        ConstructContainerDefinition(typeof(List<MilitaryCoordinationData>));
        ConstructContainerDefinition(typeof(List<EconomicNetworkData>));
        ConstructContainerDefinition(typeof(List<SpyNetworkData>));
        ConstructContainerDefinition(typeof(List<TradeTransferRecord>));
    }
}

