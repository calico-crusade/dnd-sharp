namespace DndSharp.FiveE2014;

using Abstraction;

/// <summary>
/// The constants for 5e 2014
/// </summary>
public static class Constants
{
    /// <summary>
    /// All of the <see cref="AbilityScore"/>s
    /// </summary>
    public class Scores : IStaticResource<AbilityScore>
    {
        /// <summary>
        /// Strength measures bodily power, athletic training, and the extent to which you can exert raw physical force
        /// </summary>
        public static AbilityScore Strength { get; } = new("Strength", "Strength measures bodily power, athletic training, and the extent to which you can exert raw physical force. A Strength check can model any attempt to lift, push, pull, or break something, to force your body through a space, or to otherwise apply brute force to a situation. The Athletics skill reflects aptitude in certain kinds of Strength checks.");

        /// <summary>
        /// Dexterity measures agility, reflexes, and balance.
        /// </summary>
        public static AbilityScore Dexterity { get; } = new("Dexterity", "Dexterity measures agility, reflexes, and balance. A Dexterity check can model any attempt to move nimbly, quickly, or quietly, or to keep from falling on tricky footing. The Acrobatics, Sleight of Hand, and Stealth skills reflect aptitude in certain kinds of Dexterity checks.");

        /// <summary>
        /// Constitution measures health, stamina, and vital force.
        /// </summary>
        public static AbilityScore Constitution { get; } = new("Constitution", "Constitution measures health, stamina, and vital force. Constitution checks are uncommon, and no skills apply to Constitution checks, because the endurance this ability represents is largely passive rather than involving a specific effort on the part of a character or monster.");

        /// <summary>
        /// Intelligence measures mental acuity, accuracy of recall, and the ability to reason.
        /// </summary>
        public static AbilityScore Intelligence { get; } = new("Intelligence", "Intelligence measures mental acuity, accuracy of recall, and the ability to reason. An Intelligence check comes into play when you need to draw on logic, education, memory, or deductive reasoning. The Arcana, History, Investigation, Nature, and Religion skills reflect aptitude in certain kinds of Intelligence checks.");

        /// <summary>
        /// Wisdom reflects how attuned you are to the world around you and represents perceptiveness and intuition.
        /// </summary>
        public static AbilityScore Wisdom { get; } = new("Wisdom", "Wisdom reflects how attuned you are to the world around you and represents perceptiveness and intuition. A Wisdom check might reflect an effort to read body language, understand someone's feelings, notice things about the environment, or care for an injured person. The Animal Handling, Insight, Medicine, Perception, and Survival skills reflect aptitude in certain kinds of Wisdom checks.");

        /// <summary>
        /// Charisma measures your ability to interact effectively with others.
        /// </summary>
        public static AbilityScore Charisma { get; } = new("Charisma", "Charisma measures your ability to interact effectively with others. It includes such factors as confidence and eloquence, and it can represent a charming or commanding personality. A Charisma check might arise when you try to influence or entertain others, when you try to make an impression or tell a convincing lie, or when you are navigating a tricky social situation. The Deception, Intimidation, Performance, and Persuasion skills reflect aptitude in certain kinds of Charisma checks.");

        /// <summary>
        /// All of the <see cref="AbilityScore"/>s
        /// </summary>
        public static AbilityScore[] All { get; } = 
        [
            Strength,
            Dexterity,
            Constitution,
            Intelligence,
            Wisdom,
            Charisma
        ];
    }

    /// <summary>
    /// All of the <see cref="Skill"/>s
    /// </summary>
    public class Skills : IStaticResource<Skill>
    {
        /// <summary>
        /// Your Dexterity (Acrobatics) check covers your attempt to stay on your feet in a tricky situation
        /// </summary>
        public static Skill Acrobatics { get; } = new("Acrobatics", "Your Dexterity (Acrobatics) check covers your attempt to stay on your feet in a tricky situation, such as when you're trying to run across a sheet of ice, balance on a tightrope, or stay upright on a rocking ship's deck. The GM might also call for a Dexterity (Acrobatics) check to see if you can perform acrobatic stunts, including dives, rolls, somersaults, and flips.", Scores.Dexterity);

        /// <summary>
        /// When there is any question whether you can calm down a domesticated animal, keep a mount from getting spooked, or intuit an animal's intentions
        /// </summary>
        public static Skill AnimalHandling { get; } = new("Animal Handling", "When there is any question whether you can calm down a domesticated animal, keep a mount from getting spooked, or intuit an animal's intentions, the GM might call for a Wisdom (Animal Handling) check. You also make a Wisdom (Animal Handling) check to control your mount when you attempt a risky maneuver.", Scores.Wisdom);

        /// <summary>
        /// Your Intelligence (Arcana) check measures your ability to recall lore about spells, magic items, eldritch symbols, magical traditions, the planes of existence, and the inhabitants of those planes.
        /// </summary>
        public static Skill Arcana { get; } = new("Arcana", "Your Intelligence (Arcana) check measures your ability to recall lore about spells, magic items, eldritch symbols, magical traditions, the planes of existence, and the inhabitants of those planes.", Scores.Intelligence);

        /// <summary>
        /// Your Strength (Athletics) check covers difficult situations you encounter while climbing, jumping, or swimming.
        /// </summary>
        public static Skill Athletics { get; } = new("Athletics", "Your Strength (Athletics) check covers difficult situations you encounter while climbing, jumping, or swimming.", Scores.Strength);

        /// <summary>
        /// Your Charisma (Deception) check determines whether you can convincingly hide the truth
        /// </summary>
        public static Skill Deception { get; } = new("Deception", "Your Charisma (Deception) check determines whether you can convincingly hide the truth, either verbally or through your actions. This deception can encompass everything from misleading others through ambiguity to telling outright lies. Typical situations include trying to fast- talk a guard, con a merchant, earn money through gambling, pass yourself off in a disguise, dull someone's suspicions with false assurances, or maintain a straight face while telling a blatant lie.", Scores.Charisma);

        /// <summary>
        /// Your Intelligence (History) check measures your ability to recall lore about historical events, legendary people, ancient kingdoms, past disputes, recent wars, and lost civilizations.
        /// </summary>
        public static Skill History { get; } = new("History", "Your Intelligence (History) check measures your ability to recall lore about historical events, legendary people, ancient kingdoms, past disputes, recent wars, and lost civilizations.", Scores.Intelligence);

        /// <summary>
        /// Your Wisdom (Insight) check decides whether you can determine the true intentions of a creature
        /// </summary>
        public static Skill Insight { get; } = new("Insight", "Your Wisdom (Insight) check decides whether you can determine the true intentions of a creature, such as when searching out a lie or predicting someone's next move. Doing so involves gleaning clues from body language, speech habits, and changes in mannerisms.", Scores.Wisdom);

        /// <summary>
        /// When you attempt to influence someone through overt threats, hostile actions, and physical violence
        /// </summary>
        public static Skill Intimidation { get; } = new("Intimidation", "When you attempt to influence someone through overt threats, hostile actions, and physical violence, the GM might ask you to make a Charisma (Intimidation) check. Examples include trying to pry information out of a prisoner, convincing street thugs to back down from a confrontation, or using the edge of a broken bottle to convince a sneering vizier to reconsider a decision.", Scores.Charisma);

        /// <summary>
        /// When you look around for clues and make deductions based on those clues, you make an Intelligence (Investigation) check.
        /// </summary>
        public static Skill Investigation { get; } = new("Investigation", "When you look around for clues and make deductions based on those clues, you make an Intelligence (Investigation) check. You might deduce the location of a hidden object, discern from the appearance of a wound what kind of weapon dealt it, or determine the weakest point in a tunnel that could cause it to collapse. Poring through ancient scrolls in search of a hidden fragment of knowledge might also call for an Intelligence (Investigation) check.", Scores.Intelligence);

        /// <summary>
        /// A Wisdom (Medicine) check lets you try to stabilize a dying companion or diagnose an illness.
        /// </summary>
        public static Skill Medicine { get; } = new("Medicine", "A Wisdom (Medicine) check lets you try to stabilize a dying companion or diagnose an illness.", Scores.Wisdom);

        /// <summary>
        /// Your Intelligence (Nature) check measures your ability to recall lore about terrain, plants and animals, the weather, and natural cycles.
        /// </summary>
        public static Skill Nature { get; } = new("Nature", "Your Intelligence (Nature) check measures your ability to recall lore about terrain, plants and animals, the weather, and natural cycles.", Scores.Intelligence);

        /// <summary>
        /// Your Wisdom (Perception) check lets you spot, hear, or otherwise detect the presence of something.
        /// </summary>
        public static Skill Perception { get; } = new("Perception", "Your Wisdom (Perception) check lets you spot, hear, or otherwise detect the presence of something. It measures your general awareness of your surroundings and the keenness of your senses. For example, you might try to hear a conversation through a closed door, eavesdrop under an open window, or hear monsters moving stealthily in the forest. Or you might try to spot things that are obscured or easy to miss, whether they are orcs lying in ambush on a road, thugs hiding in the shadows of an alley, or candlelight under a closed secret door.", Scores.Wisdom);

        /// <summary>
        /// Your Charisma (Performance) check determines how well you can delight an audience with music, dance, acting, storytelling, or some other form of entertainment.
        /// </summary>
        public static Skill Performance { get; } = new("Performance", "Your Charisma (Performance) check determines how well you can delight an audience with music, dance, acting, storytelling, or some other form of entertainment.", Scores.Charisma);

        /// <summary>
        /// When you attempt to influence someone or a group of people with tact, social graces, or good nature.
        /// </summary>
        public static Skill Persuasion { get; } = new("Persuasion", "When you attempt to influence someone or a group of people with tact, social graces, or good nature, the GM might ask you to make a Charisma (Persuasion) check. Typically, you use persuasion when acting in good faith, to foster friendships, make cordial requests, or exhibit proper etiquette. Examples of persuading others include convincing a chamberlain to let your party see the king, negotiating peace between warring tribes, or inspiring a crowd of townsfolk.", Scores.Charisma);

        /// <summary>
        /// Your Intelligence (Religion) check measures your ability to recall lore about deities, rites and prayers, religious hierarchies, holy symbols, and the practices of secret cults.
        /// </summary>
        public static Skill Religion { get; } = new("Religion", "Your Intelligence (Religion) check measures your ability to recall lore about deities, rites and prayers, religious hierarchies, holy symbols, and the practices of secret cults.", Scores.Intelligence);

        /// <summary>
        /// Whenever you attempt an act of legerdemain or manual trickery make a Dexterity (Sleight of Hand) check.
        /// </summary>
        public static Skill SleightOfHand { get; } = new("Sleight of Hand", "Whenever you attempt an act of legerdemain or manual trickery, such as planting something on someone else or concealing an object on your person, make a Dexterity (Sleight of Hand) check. The GM might also call for a Dexterity (Sleight of Hand) check to determine whether you can lift a coin purse off another person or slip something out of another person's pocket.", Scores.Dexterity);

        /// <summary>
        /// Make a Dexterity (Stealth) check when you attempt to conceal yourself from enemies, slink past guards, slip away without being noticed, or sneak up on someone without being seen or heard.
        /// </summary>
        public static Skill Stealth { get; } = new("Stealth", "Make a Dexterity (Stealth) check when you attempt to conceal yourself from enemies, slink past guards, slip away without being noticed, or sneak up on someone without being seen or heard.", Scores.Dexterity);

        /// <summary>
        /// Your Wisdom (Survival) check to follow tracks, hunt wild game, guide your group through frozen wastelands, identify signs that owlbears live nearby, predict the weather, or avoid quicksand and other natural hazards.
        /// </summary>
        public static Skill Survival { get; } = new("Survival", "The GM might ask you to make a Wisdom (Survival) check to follow tracks, hunt wild game, guide your group through frozen wastelands, identify signs that owlbears live nearby, predict the weather, or avoid quicksand and other natural hazards.", Scores.Wisdom);

        /// <summary>
        /// All of the <see cref="Skill"/>s
        /// </summary>
        public static Skill[] All { get; } =
        [
            Acrobatics,
            AnimalHandling,
            Arcana,
            Athletics,
            Deception,
            History,
            Insight,
            Intimidation,
            Investigation,
            Medicine,
            Nature,
            Perception,
            Performance,
            Persuasion,
            Religion,
            SleightOfHand,
            Stealth,
            Survival
        ];
    }

    /// <summary>
    /// All of the <see cref="WeaponProperty"/>s
    /// </summary>
    public class WeaponProperties : IStaticResource<WeaponProperty>
    {
        /// <summary>
        /// You can use a weapon that has the ammunition property to make a ranged attack only if you have ammunition to fire from the weapon.
        /// </summary>
        public static WeaponProperty Ammunition { get; } = new("Ammunition", "You can use a weapon that has the ammunition property to make a ranged attack only if you have ammunition to fire from the weapon. Each time you attack with the weapon, you expend one piece of ammunition. Drawing the ammunition from a quiver, case, or other container is part of the attack (you need a free hand to load a one-handed weapon). At the end of the battle, you can recover half your expended ammunition by taking a minute to search the battlefield. If you use a weapon that has the ammunition property to make a melee attack, you treat the weapon as an improvised weapon (see \\\"Improvised Weapons\\\" later in the section). A sling must be loaded to deal any damage when used in this way.");

        /// <summary>
        /// When making an attack with a finesse weapon, you use your choice of your Strength or Dexterity modifier for the attack and damage rolls.
        /// </summary>
        public static WeaponProperty Finesse { get; } = new("Finesse", "When making an attack with a finesse weapon, you use your choice of your Strength or Dexterity modifier for the attack and damage rolls. You must use the same modifier for both rolls.");

        /// <summary>
        /// Small creatures have disadvantage on attack rolls with heavy weapons.
        /// </summary>
        public static WeaponProperty Heavy { get; } = new("Heavy", "Small creatures have disadvantage on attack rolls with heavy weapons. A heavy weapon's size and bulk make it too large for a Small creature to use effectively.");

        /// <summary>
        /// A light weapon is small and easy to handle, making it ideal for use when fighting with two weapons.
        /// </summary>
        public static WeaponProperty Light { get; } = new("Light", "A light weapon is small and easy to handle, making it ideal for use when fighting with two weapons.");

        /// <summary>
        /// Because of the time required to load this weapon, you can fire only one piece of ammunition from it when you use an action, bonus action, or reaction to fire it, regardless of the number of attacks you can normally make.
        /// </summary>
        public static WeaponProperty Loading { get; } = new("Loading", "Because of the time required to load this weapon, you can fire only one piece of ammunition from it when you use an action, bonus action, or reaction to fire it, regardless of the number of attacks you can normally make.");

        /// <summary>
        /// This weapon adds 5 feet to your reach when you attack with it, as well as when determining your reach for opportunity attacks with it.
        /// </summary>
        public static WeaponProperty Reach { get; } = new("Reach", "This weapon adds 5 feet to your reach when you attack with it, as well as when determining your reach for opportunity attacks with it.");

        /// <summary>
        /// A weapon with the special property has unusual rules governing its use, explained in the weapon's description.
        /// </summary>
        public static WeaponProperty Special { get; } = new("Special", "A weapon with the special property has unusual rules governing its use, explained in the weapon's description.");

        /// <summary>
        /// If a weapon has the thrown property, you can throw the weapon to make a ranged attack. If the weapon is a melee weapon, you use the same ability modifier for that attack roll and damage roll that you would use for a melee attack with the weapon. 
        /// </summary>
        public static WeaponProperty Thrown { get; } = new("Thrown", "If a weapon has the thrown property, you can throw the weapon to make a ranged attack. If the weapon is a melee weapon, you use the same ability modifier for that attack roll and damage roll that you would use for a melee attack with the weapon. For example, if you throw a hand-axe, you use your Strength, but if you throw a dagger, you can use either your Strength or your Dexterity, since the dagger has the finesse property.");

        /// <summary>
        /// This weapon requires two hands to use.
        /// </summary>
        public static WeaponProperty TwoHanded { get; } = new("Two-Handed", "This weapon requires two hands to use.");

        /// <summary>
        /// This weapon can be used with one or two hands.
        /// </summary>
        public static WeaponProperty Versatile { get; } = new("Versatile", "This weapon can be used with one or two hands. A damage value in parentheses appears with the property--the damage when the weapon is used with two hands to make a melee attack.");

        /// <summary>
        /// Monks gain several benefits while unarmed or wielding only monk weapons while they aren't wearing armor or wielding shields.
        /// </summary>
        public static WeaponProperty Monk { get; } = new("Monk", "Monks gain several benefits while unarmed or wielding only monk weapons while they aren't wearing armor or wielding shields.");

        /// <summary>
        /// All of the <see cref="WeaponProperty"/>s
        /// </summary>
        public static WeaponProperty[] All { get; } =
        [
            Ammunition,
            Finesse,
            Heavy,
            Light,
            Loading,
            Reach,
            Special,
            Thrown,
            TwoHanded,
            Versatile,
            Monk
        ];
    }

    /// <summary>
    /// All of the <see cref="EquipmentCategory"/>s
    /// </summary>
    public class EquipmentCategories : IStaticResource<EquipmentCategory>
    {
        public static EquipmentCategory Weapons { get; } = new("Weapons");

        public static EquipmentCategory Armor { get; } = new("Armor");

        public static EquipmentCategory AdventuringGear { get; } = new("Adventuring Gear");

        public static EquipmentCategory Ammunition { get; } = new("Ammunition");

        public static EquipmentCategory Tools { get; } = new("Tools");

        public static EquipmentCategory MountsAndVehicles { get; } = new("Mounts and Vehicles");

        public static EquipmentCategory SimpleWeapons { get; } = new("Simple Weapons");

        public static EquipmentCategory MartialWeapons { get; } = new("Martial Weapon");

        public static EquipmentCategory MeleeWeapons { get; } = new("Melee Weapons");

        public static EquipmentCategory RangedWeapons { get; } = new("Ranged Weapons");

        public static EquipmentCategory SimpleMeleeWeapons { get; } = new("Simple Melee Weapons");

        public static EquipmentCategory SimpleRangedWeapons { get; } = new("Simple Ranged Weapons");

        public static EquipmentCategory MartialMeleeWeapons { get; } = new("Martial Melee Weapons");

        public static EquipmentCategory MartialRangedWeapons { get; } = new("Martial Ranged Weapons");

        public static EquipmentCategory LightArmor { get; } = new("Light Armor");

        public static EquipmentCategory MediumArmor { get; } = new("Medium Armor");

        public static EquipmentCategory HeavyArmor { get; } = new("Heavy Armor");

        public static EquipmentCategory Shields { get; } = new("Shields");

        public static EquipmentCategory StandardGear { get; } = new("Standard Gear");

        public static EquipmentCategory Kits { get; } = new("Kits");

        public static EquipmentCategory EquipmentPacks { get; } = new("Equipment Packs");

        public static EquipmentCategory ArtisansTools { get; } = new("Artisan's Tools");

        public static EquipmentCategory GamingSets { get; } = new("Gaming Sets");

        public static EquipmentCategory MusicalInstruments { get; } = new("Musical Instruments");

        public static EquipmentCategory OtherTools { get; } = new("Other Tools");

        public static EquipmentCategory MountsAndOtherAnimals { get; } = new("Mounts and Other Animals");

        public static EquipmentCategory TackHarnessAndDrawnVehicles { get; } = new("Tack, Harness, and Drawn Vehicles");

        public static EquipmentCategory LandVehicles { get; } = new ("Land Vehicles");

        public static EquipmentCategory WaterborneVehicles { get; } = new("Waterborne Vehicles");

        public static EquipmentCategory ArcaneFoci { get; } = new("Arcane Foci");

        public static EquipmentCategory DivineFoci { get; } = new("Divine Foci");

        public static EquipmentCategory HolySymbols { get; } = new("Holy Symbols");

        public static EquipmentCategory WondrousItems { get; } = new("Wondrous Items");

        public static EquipmentCategory Rods { get; } = new("Rods");

        public static EquipmentCategory Potions { get; } = new("Potions");

        public static EquipmentCategory Rings { get; } = new("Rings");

        public static EquipmentCategory Scrolls { get; } = new("Scrolls");

        public static EquipmentCategory Staffs { get; } = new("Staffs");

        public static EquipmentCategory Wands { get; } = new("Wands");

        public static EquipmentCategory[] All { get; } =
        [
            Weapons,
            Armor,
            AdventuringGear,
            Ammunition,
            Tools,
            MountsAndVehicles,
            SimpleWeapons,
            MartialWeapons,
            MeleeWeapons,
            RangedWeapons,
            SimpleMeleeWeapons,
            SimpleRangedWeapons,
            MartialMeleeWeapons,
            MartialRangedWeapons,
            LightArmor,
            MediumArmor,
            HeavyArmor,
            Shields,
            StandardGear,
            Kits,
            EquipmentPacks,
            ArtisansTools,
            GamingSets,
            MusicalInstruments,
            OtherTools,
            MountsAndOtherAnimals,
            TackHarnessAndDrawnVehicles,
            LandVehicles,
            WaterborneVehicles,
            ArcaneFoci,
            DivineFoci,
            HolySymbols,
            WondrousItems,
            Rods,
            Potions,
            Rings,
            Scrolls,
            Staffs,
            Wands
        ];
    }
}