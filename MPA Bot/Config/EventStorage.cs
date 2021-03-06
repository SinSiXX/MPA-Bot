﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using RestSharp;
using Newtonsoft.Json;

namespace MPA_Bot.Modules.PSO2
{
    public class EventStorage
    {
        [JsonProperty("events")]
        public Dictionary<int, Event> ActiveEvents { get; set; }

        [JsonIgnore]
        private Dictionary<int, Event> OldEvents { get; set; }

        public static EventStorage Load()
        {
            if (File.Exists("events.json"))
            {
                var json = File.ReadAllText("events.json");
                return JsonConvert.DeserializeObject<EventStorage>(json);
            }
            var events = new EventStorage();
            events.Save();

            return events;
        }

        public void Save()
        {
            JsonStorage.SerializeObjectToFile(this, "events.json").Wait();
        }

        public EventStorage()
        {
            ActiveEvents = new Dictionary<int, Event>();
            OldEvents = new Dictionary<int, Event>();

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (OldEvents.All(x =>
                        {
                            Event v;

                            if (ActiveEvents.TryGetValue(x.Key, out v))
                                return x.Value.Equals(v);

                            return false;
                        }))
                        {
                            OldEvents = new Dictionary<int, Event>(ActiveEvents);

                            Save();

                        }
                    }
                    catch (Exception ex)
                    {
                        // add logging pls
                        Console.WriteLine($"Error saving!\n{ex.Message}\n{ex.Source}");
                    }

                    await Task.Delay(1000 * 60);
                }
            });
        }
    }

    public class Event
    {
        public string Description;
        public List<Player> Players = new List<Player>();
        public List<Player> WaitList = new List<Player>();
        public int MaxPlayers = 12;
        public int Block = 201;
        public ulong Creator = 0;
        public bool Locked = false;
        public int Password = 0;

        [JsonIgnore]
        public List<int> Party = new List<int>();

        public string HeadCount()
        {
            return $"{Players.Count().ToString("00")}/{MaxPlayers.ToString("00")}{((Players.Count() >= MaxPlayers) ? " [FULL]" : "")}";
        }

        public bool AddPlayer(IUser user, string className = "", bool leader = false)
        {
            if (ContainsPlayer(user))
                return false;

            if (Players.Count() >= MaxPlayers)
            {
                //WaitList.Add(new Player() { UserId = user.Id, Class = className, Leader = leader });
                return false;
            }

            Players.Add(new Player() { UserId = user.Id, Class = className, Leader = leader });
            Party.Clear();

            return true;
        }

        public void AddPlayer(string name, string className = "", bool leader = false)
        {
            Players.Add(new Player() { PSOName = name, Class = className, Leader = leader });
            Party.Clear();
        }

        public List<Player> UpdateWaitlist()
        {
            if (WaitList.Count() == 0)
                return null;

            var tmp = WaitList.Take(MaxPlayers - Players.Count()).ToList();

            Players.AddRange(tmp);
            tmp.ForEach(x => WaitList.Remove(x));

            return tmp;
        }

        public bool RemovePlayer(IUser user)
        {
            if (!ContainsPlayer(user))
                return false;

            var player = Players.FirstOrDefault(x => x.UserId == user.Id);
            Players.Remove(player);
            Party.Clear();

            return true;
        }

        public void RemovePlayer(string name)
        {
            var player = Players.FirstOrDefault(x => x.PSOName.ToLower() == name.ToLower());
            Players.Remove(player);
            Party.Clear();
        }

        public Player GetPlayer(IUser user)
        {
            if (!ContainsPlayer(user))
                return null;

            Player player;

            if (WaitList.Select(x => x.UserId).Contains(user.Id))
                player = WaitList.FirstOrDefault(x => x.UserId == user.Id);
            else
                player = Players.FirstOrDefault(x => x.UserId == user.Id);

            Party.Clear();

            return player;
        }

        public Player GetPlayer(string name)
        {
            if (ContainsPlayer(name) == false || ContainsPlayer(name) == null)
                return null;

            var player = Players.FirstOrDefault(x => x.PSOName.ToLower() == name.ToLower());
            return player;
        }

        public void SetClass(IUser user, string className)
        {
            var player = Players.FirstOrDefault(x => x.UserId == user.Id);
            player.SetClass(className);
        }

        public void SetClass(string name, string className)
        {
            var player = Players.FirstOrDefault(x => x.PSOName.ToLower() == name.ToLower());
            player.SetClass(className);
        }

        public bool ContainsPlayer(IUser user)
        {
            return Players.Select(x => x.UserId).Contains(user.Id) || WaitList.Select(x => x.UserId).Contains(user.Id);
        }

        public bool? ContainsPlayer(string name)
        {
            var count = Players.Count(x => x.PSOName.ToLower() == name.ToLower());

            switch (count)
            {
                case 0:
                    return false;
                case 1:
                    return true;
                default:
                    return null;
            }
        }

        public void SetLeaders(IEnumerable<IUser> users)
        {
            ClearLeaders();

            foreach (var u in users)
            {
                GetPlayer(u).SetLeader();
            }
        }

        public void ClearLeaders()
        {
            Players.ForEach(x => x.RemoveLeader());
            Party.Clear();
        }
    }

    public class Player
    {
        public ulong UserId;
        public string PSOName = "";
        public bool Discord = true;
        public string Class;
        public bool Leader = false;

        public void SetClass(string className)
        {
            Class = className;
        }

        public void SetLeader()
        {
            Leader = true;
        }

        public void RemoveLeader()
        {
            Leader = false;
        }
    }
}
