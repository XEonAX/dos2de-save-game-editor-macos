using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dos2SaveEditor.Core.Models;
using Dos2SaveEditor.Core.Services;
using Dos2SaveEditor.Core.Utils;
using System;

namespace Dos2SaveEditor.ViewModels;

public partial class CharacterEditViewModel : ViewModelBase
{
    private readonly ISavegameService _saveService;
    private readonly SavegameInfo _saveInfo;
    public Character Character { get; }

    [ObservableProperty] private decimal _vitality;
    [ObservableProperty] private decimal _vitalityMax;
    [ObservableProperty] private decimal _armor;
    [ObservableProperty] private decimal _armorMax;
    [ObservableProperty] private decimal _magicArmor;
    [ObservableProperty] private decimal _magicArmorMax;
    [ObservableProperty] private decimal _experience;
    [ObservableProperty] private decimal _level;
    [ObservableProperty] private decimal _attributePoints;
    [ObservableProperty] private decimal _combatAbilityPoints;
    [ObservableProperty] private decimal _civilAbilityPoints;
    [ObservableProperty] private decimal _talentPoints;

    // 6 attributes
    [ObservableProperty] private decimal _strength;
    [ObservableProperty] private decimal _dexterity;
    [ObservableProperty] private decimal _intelligence;
    [ObservableProperty] private decimal _constitution;
    [ObservableProperty] private decimal _memory;
    [ObservableProperty] private decimal _wits;

    // Combat abilities (18)
    [ObservableProperty] private decimal _dualWielding;
    [ObservableProperty] private decimal _twoHanded;
    [ObservableProperty] private decimal _ranged;
    [ObservableProperty] private decimal _singleHanded;
    [ObservableProperty] private decimal _shield;
    [ObservableProperty] private decimal _painReflection;
    [ObservableProperty] private decimal _leadership;
    [ObservableProperty] private decimal _perseverance;
    [ObservableProperty] private decimal _warfare;
    [ObservableProperty] private decimal _aerotheurge;
    [ObservableProperty] private decimal _huntsman;
    [ObservableProperty] private decimal _scoundrel;
    [ObservableProperty] private decimal _geomancer;
    [ObservableProperty] private decimal _hydrosophist;
    [ObservableProperty] private decimal _summoning;
    [ObservableProperty] private decimal _polymorph;
    [ObservableProperty] private decimal _necromancy;
    [ObservableProperty] private decimal _pyrokinetic;

    // Civil abilities (7)
    [ObservableProperty] private decimal _bartering;
    [ObservableProperty] private decimal _persuasion;
    [ObservableProperty] private decimal _luckyCharm;
    [ObservableProperty] private decimal _loremaster;
    [ObservableProperty] private decimal _telekinesis;
    [ObservableProperty] private decimal _sneaking;
    [ObservableProperty] private decimal _thievery;

    public CharacterEditViewModel(ISavegameService saveService, SavegameInfo saveInfo, Character character)
    {
        _saveService = saveService;
        _saveInfo = saveInfo;
        Character = character;
        LoadFromCharacter();
    }

    private void LoadFromCharacter()
    {
        Vitality = Character.Vitality;
        VitalityMax = Character.VitalityMax;
        Armor = Character.Armor;
        ArmorMax = Character.ArmorMax;
        MagicArmor = Character.MagicArmor;
        MagicArmorMax = Character.MagicArmorMax;
        Experience = Character.Experience;
        Level = Character.Level;
        AttributePoints = Character.AttributePoints;
        CombatAbilityPoints = Character.CombatAbilityPoints;
        CivilAbilityPoints = Character.CivilAbilityPoints;
        TalentPoints = Character.TalentPoints;

        // Attributes
        Strength = Character.Attributes[0];
        Dexterity = Character.Attributes[1];
        Intelligence = Character.Attributes[2];
        Constitution = Character.Attributes[3];
        Memory = Character.Attributes[4];
        Wits = Character.Attributes[5];

        // Combat abilities (indices 0-17)
        DualWielding = GetAbility(0);
        TwoHanded = GetAbility(1);
        Ranged = GetAbility(2);
        SingleHanded = GetAbility(3);
        Shield = GetAbility(4);
        PainReflection = GetAbility(5);
        Leadership = GetAbility(6);
        Perseverance = GetAbility(7);
        Warfare = GetAbility(8);
        Aerotheurge = GetAbility(9);
        Huntsman = GetAbility(10);
        Scoundrel = GetAbility(11);
        Geomancer = GetAbility(12);
        Hydrosophist = GetAbility(13);
        Summoning = GetAbility(14);
        Polymorph = GetAbility(15);
        Necromancy = GetAbility(16);
        Pyrokinetic = GetAbility(17);

        // Civil abilities (indices 18-24)
        Bartering = GetAbility(18);
        Persuasion = GetAbility(19);
        LuckyCharm = GetAbility(20);
        Loremaster = GetAbility(21);
        Telekinesis = GetAbility(22);
        Sneaking = GetAbility(23);
        Thievery = GetAbility(24);
    }

    private decimal GetAbility(int idx) =>
        idx < Character.Abilities.Length ? Character.Abilities[idx] : 0;

    [RelayCommand]
    private void Apply()
    {
        try
        {
            Character.Vitality = (int)Vitality;
            Character.VitalityMax = (int)VitalityMax;
            Character.Armor = (int)Armor;
            Character.ArmorMax = (int)ArmorMax;
            Character.MagicArmor = (int)MagicArmor;
            Character.MagicArmorMax = (int)MagicArmorMax;
            Character.Experience = (uint)Experience;
            Character.AttributePoints = (int)AttributePoints;
            Character.CombatAbilityPoints = (int)CombatAbilityPoints;
            Character.CivilAbilityPoints = (int)CivilAbilityPoints;
            Character.TalentPoints = (int)TalentPoints;

            Character.Attributes[0] = (int)Strength;
            Character.Attributes[1] = (int)Dexterity;
            Character.Attributes[2] = (int)Intelligence;
            Character.Attributes[3] = (int)Constitution;
            Character.Attributes[4] = (int)Memory;
            Character.Attributes[5] = (int)Wits;

            SetAbility(0, DualWielding);
            SetAbility(1, TwoHanded);
            SetAbility(2, Ranged);
            SetAbility(3, SingleHanded);
            SetAbility(4, Shield);
            SetAbility(5, PainReflection);
            SetAbility(6, Leadership);
            SetAbility(7, Perseverance);
            SetAbility(8, Warfare);
            SetAbility(9, Aerotheurge);
            SetAbility(10, Huntsman);
            SetAbility(11, Scoundrel);
            SetAbility(12, Geomancer);
            SetAbility(13, Hydrosophist);
            SetAbility(14, Summoning);
            SetAbility(15, Polymorph);
            SetAbility(16, Necromancy);
            SetAbility(17, Pyrokinetic);
            SetAbility(18, Bartering);
            SetAbility(19, Persuasion);
            SetAbility(20, LuckyCharm);
            SetAbility(21, Loremaster);
            SetAbility(22, Telekinesis);
            SetAbility(23, Sneaking);
            SetAbility(24, Thievery);

            _saveService.UpdateCharacter(_saveInfo, Character);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Apply failed: {ex.Message}");
            throw;
        }
    }

    private void SetAbility(int idx, decimal value)
    {
        if (idx < Character.Abilities.Length)
            Character.Abilities[idx] = (int)value;
    }
}
