using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace WidgUI
{
    public static class ProfileService
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "widgUI");

        private static readonly string ProfilesDir = Path.Combine(ConfigDir, "profiles");
        private static readonly string LastProfileFile = Path.Combine(ConfigDir, "last_profile.txt");

        public static string ProfilesDirectory
        {
            get { return ProfilesDir; }
        }

        public static void EnsureDirectories()
        {
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }

            if (!Directory.Exists(ProfilesDir))
            {
                Directory.CreateDirectory(ProfilesDir);
            }
        }

        public static void SaveProfile(LayoutProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
            {
                throw new ArgumentException("El perfil necesita un nombre.");
            }

            EnsureDirectories();
            profile.SavedAt = DateTime.Now.ToString("o");
            string filePath = GetProfileFilePath(profile.Name);

            using (FileStream stream = File.Create(filePath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LayoutProfile));
                serializer.WriteObject(stream, profile);
            }

            SaveLastProfileName(profile.Name);
        }

        public static LayoutProfile LoadProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string filePath = GetProfileFilePath(name);
            if (!File.Exists(filePath))
            {
                return null;
            }

            using (FileStream stream = File.OpenRead(filePath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LayoutProfile));
                return (LayoutProfile)serializer.ReadObject(stream);
            }
        }

        public static List<ProfileSummary> ListProfiles()
        {
            EnsureDirectories();
            List<ProfileSummary> profiles = new List<ProfileSummary>();

            foreach (string file in Directory.GetFiles(ProfilesDir, "*.json"))
            {
                try
                {
                    LayoutProfile profile = LoadProfileFromFile(file);
                    if (profile != null)
                    {
                        profiles.Add(new ProfileSummary
                        {
                            Name = profile.Name,
                            SavedAt = profile.SavedAt,
                            FilePath = file
                        });
                    }
                }
                catch
                {
                }
            }

            return profiles
                .OrderByDescending(p => p.SavedAt ?? string.Empty)
                .ToList();
        }

        public static void DeleteProfile(string name)
        {
            string filePath = GetProfileFilePath(name);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (string.Equals(GetLastProfileName(), name, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(LastProfileFile))
                {
                    File.Delete(LastProfileFile);
                }
            }
        }

        public static void SaveLastProfileName(string name)
        {
            EnsureDirectories();
            File.WriteAllText(LastProfileFile, name, Encoding.UTF8);
        }

        public static string GetLastProfileName()
        {
            try
            {
                if (File.Exists(LastProfileFile))
                {
                    return File.ReadAllText(LastProfileFile, Encoding.UTF8).Trim();
                }
            }
            catch
            {
            }

            return null;
        }

        public static string SanitizeProfileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name.Trim();
        }

        private static string GetProfileFilePath(string name)
        {
            return Path.Combine(ProfilesDir, SanitizeProfileName(name) + ".json");
        }

        private static LayoutProfile LoadProfileFromFile(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LayoutProfile));
                return (LayoutProfile)serializer.ReadObject(stream);
            }
        }
    }

    public class ProfileSummary
    {
        public string Name { get; set; }
        public string SavedAt { get; set; }
        public string FilePath { get; set; }

        public string DisplayDate
        {
            get
            {
                DateTime parsed;
                if (DateTime.TryParse(SavedAt, out parsed))
                {
                    return parsed.ToLocalTime().ToString("g");
                }

                return SavedAt ?? string.Empty;
            }
        }
    }
}
