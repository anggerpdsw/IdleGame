using System.Collections.Generic;

namespace IdleDefenseSurvival.Pet
{
    /// <summary>
    /// Public interface for Pet system, exposed via ServiceLocator.
    /// Allows other systems to interact with pets without tight coupling.
    /// </summary>
    public interface IPetService
    {
        /// <summary>
        /// Get all currently equipped and active pets.
        /// </summary>
        List<PetRuntime> GetActivePets();

        /// <summary>
        /// Equip a pet by instance ID.
        /// Returns false if pet not owned or already equipped.
        /// </summary>
        bool EquipPet(string instanceId);

        /// <summary>
        /// Unequip a pet by instance ID.
        /// Returns false if pet not currently equipped.
        /// </summary>
        bool UnequipPet(string instanceId);

        /// <summary>
        /// Get pet definition by pet ID.
        /// Returns null if pet definition not found.
        /// </summary>
        PetDefinition GetPetDefinition(string petId);

        /// <summary>
        /// Get owned pet runtime instance by instance ID.
        /// Returns null if not owned.
        /// </summary>
        PetRuntime GetOwnedPet(string instanceId);

        /// <summary>
        /// Grant a new pet to player inventory.
        /// Returns instance ID of new pet.
        /// </summary>
        string GrantPet(string petId, int level = 1);

        /// <summary>
        /// Check if emergency mode is active (any equipped pet in emergency).
        /// </summary>
        bool IsEmergencyModeActive();
    }
}
