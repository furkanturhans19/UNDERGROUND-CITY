using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace OpenCity
{
	public enum EncounterType { PoliceCheck, GangAmbush, StreetDeal, FoundCash }
	public enum MissionType { Delivery, Heist, Tail }

	[Serializable] public class Vehicle { public string id; public string name; public int price; public float speed; public float stealth; }
	[Serializable] public class PlayerProfile { public string currentLocationId; public int cash = 1500; public int wanted = 0; public int personalPower = 0; public Vehicle currentVehicle; }

	[Serializable] public class District { public string id; public string displayName; public int danger; }
	[Serializable] public class LocationNode
	{
		public string id;
		public string displayName;
		public string districtId;
		public List<string> neighbors = new List<string>();
		public bool hasGarage;
		public bool hasShop;
		public bool hasBank;
	}

	[Serializable] public class Mission
	{
		public string id;
		public MissionType type;
		public string fromLocationId;
		public string toLocationId;
		public int reward;
		public int risk;
		public bool accepted;
		public bool completed;
	}

	public static class PowerMath
	{
		public static float Mult(int personalPower)
		{
			return 0.75f * (1f - (1f / (1f + personalPower / 50f)));
		}
	}

	public class EncounterSystem
	{
		private System.Random rng = new System.Random();
		public bool TryGenerateEncounter(int districtDanger, int wanted, out EncounterType type)
		{
			int chance = Mathf.Clamp(districtDanger / 2 + wanted / 2, 0, 90);
			bool occur = rng.Next(0, 100) < chance;
			type = occur ? WeightedPick(wanted) : default;
			return occur;
		}
		private EncounterType WeightedPick(int wanted)
		{
			int roll = UnityEngine.Random.Range(0, 100);
			if (wanted > 60)
			{
				if (roll < 50) return EncounterType.PoliceCheck;
				if (roll < 85) return EncounterType.GangAmbush;
				return EncounterType.StreetDeal;
			}
			if (roll < 20) return EncounterType.FoundCash;
			if (roll < 50) return EncounterType.StreetDeal;
			if (roll < 75) return EncounterType.PoliceCheck;
			return EncounterType.GangAmbush;
		}
		public bool TryPoliceRaid(int wanted) { return wanted > 80 && UnityEngine.Random.value < 0.35f; }
	}

	public class MissionGenerator
	{
		private System.Random rng = new System.Random();
		public Mission CreateDelivery(List<string> locs)
		{
			string from = locs[rng.Next(locs.Count)];
			string to = locs[rng.Next(locs.Count)];
			while (to == from) to = locs[rng.Next(locs.Count)];
			int risk = rng.Next(10, 60);
			int reward = Mathf.RoundToInt(150 + risk * 8f);
			return new Mission { id = Guid.NewGuid().ToString(), type = MissionType.Delivery, fromLocationId = from, toLocationId = to, risk = risk, reward = reward };
		}
		public Mission CreateHeist(List<string> locs)
		{
			string bank = locs[rng.Next(locs.Count)];
			int risk = rng.Next(40, 90);
			int reward = Mathf.RoundToInt(800 + risk * 15f);
			return new Mission { id = Guid.NewGuid().ToString(), type = MissionType.Heist, fromLocationId = bank, toLocationId = bank, risk = risk, reward = reward };
		}
		public Mission CreateTail(List<string> locs)
		{
			string start = locs[rng.Next(locs.Count)];
			string end = locs[rng.Next(locs.Count)];
			while (end == start) end = locs[rng.Next(locs.Count)];
			int risk = rng.Next(20, 70);
			int reward = Mathf.RoundToInt(300 + risk * 10f);
			return new Mission { id = Guid.NewGuid().ToString(), type = MissionType.Tail, fromLocationId = start, toLocationId = end, risk = risk, reward = reward };
		}
	}

	public static class Bootstrap
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Init()
		{
			// Canvas
			var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			var canvas = canvasGO.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			var scaler = canvasGO.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1080, 1920);

			// Texts
			Text CreateText(string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize)
			{
				var go = new GameObject(name, typeof(Text));
				go.transform.SetParent(canvasGO.transform, false);
				var t = go.GetComponent<Text>();
				t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
				t.color = Color.white;
				t.alignment = TextAnchor.UpperLeft;
				t.fontSize = fontSize;
				var rt = go.GetComponent<RectTransform>();
				rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = new Vector2(16, 16); rt.offsetMax = new Vector2(-16, -16);
				return t;
			}
			Button CreateButton(string name, string label, Vector2 anchorMin, Vector2 anchorMax)
			{
				var go = new GameObject(name, typeof(Image), typeof(Button));
				go.transform.SetParent(canvasGO.transform, false);
				var img = go.GetComponent<Image>(); img.color = new Color(0, 0, 0, 0.5f);
				var rt = go.GetComponent<RectTransform>();
				rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = new Vector2(16, 16); rt.offsetMax = new Vector2(-16, -16);
				var textGO = new GameObject("Text", typeof(Text));
				textGO.transform.SetParent(go.transform, false);
				var txt = textGO.GetComponent<Text>();
				txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
				txt.text = label; txt.alignment = TextAnchor.MiddleCenter; txt.color = Color.white; txt.fontSize = 36;
				var trt = textGO.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
				return go.GetComponent<Button>();
			}

			var locationText = CreateText("LocationText", new Vector2(0f, 0.88f), new Vector2(1f, 1f), 40);
			var statusText = CreateText("StatusText", new Vector2(0f, 0.55f), new Vector2(1f, 0.86f), 30);
			var primaryBtn = CreateButton("PrimaryBtn", "Primary", new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.48f));
			var secondaryBtn = CreateButton("SecondaryBtn", "Secondary", new Vector2(0.05f, 0.27f), new Vector2(0.95f, 0.37f));
			var mapBtn = CreateButton("MapBtn", "Map", new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.26f));

			// Game manager
			var gmGO = new GameObject("GameManager");
			var gm = gmGO.AddComponent<GameManager>();
			gm.BindUI(locationText, statusText, primaryBtn, secondaryBtn, mapBtn);
		}
	}

	public class GameManager : MonoBehaviour
	{
		private Text locationText, statusText;
		private Button primaryBtn, secondaryBtn, mapBtn;

		private readonly List<District> districts = new List<District>();
		private readonly List<LocationNode> locations = new List<LocationNode>();
		private PlayerProfile player;
		private EncounterSystem encounter;
		private MissionGenerator missionGen;
		private Mission activeMission;
		private List<Mission> availableMissions = new List<Mission>();

		public void BindUI(Text loc, Text status, Button primary, Button secondary, Button map)
		{
			locationText = loc; statusText = status; primaryBtn = primary; secondaryBtn = secondary; mapBtn = map;
		}

		private void Start()
		{
			player = new PlayerProfile { currentVehicle = new Vehicle { id = "civic", name = "Compact", price = 0, speed = 1.0f, stealth = 0.2f } };
			encounter = new EncounterSystem();
			missionGen = new MissionGenerator();
			BootstrapWorld();
			SpawnPlayerAt(locations.First().id);
			RefreshMissions();
			DrawIdle();
		}

		private void BootstrapWorld()
		{
			districts.Add(new District { id = "downtown", displayName = "Downtown", danger = 35 });
			districts.Add(new District { id = "harbor", displayName = "Liman", danger = 55 });
			districts.Add(new District { id = "suburbs", displayName = "Banliyö", danger = 15 });
			locations.Add(new LocationNode { id = "dt_square", displayName = "Şehir Meydanı", districtId = "downtown", hasShop = true });
			locations.Add(new LocationNode { id = "bank_dt", displayName = "Merkez Bankası", districtId = "downtown", hasBank = true });
			locations.Add(new LocationNode { id = "harbor_pier", displayName = "Rıhtım 3", districtId = "harbor" });
			locations.Add(new LocationNode { id = "garage_sb", displayName = "Banliyö Garajı", districtId = "suburbs", hasGarage = true });
			Link("dt_square", "bank_dt"); Link("dt_square", "harbor_pier"); Link("dt_square", "garage_sb");
		}
		private void Link(string a, string b)
		{
			var A = locations.First(l => l.id == a); var B = locations.First(l => l.id == b);
			if (!A.neighbors.Contains(b)) A.neighbors.Add(b);
			if (!B.neighbors.Contains(a)) B.neighbors.Add(a);
		}
		private District CurrentDistrict() => districts.First(d => d.id == locations.First(l => l.id == player.currentLocationId).districtId);
		private void SpawnPlayerAt(string id) { player.currentLocationId = id; }

		private void RefreshMissions()
		{
			availableMissions.Clear();
			var locIds = locations.Select(l => l.id).ToList();
			availableMissions.Add(missionGen.CreateDelivery(locIds));
			availableMissions.Add(missionGen.CreateHeist(locIds));
			availableMissions.Add(missionGen.CreateTail(locIds));
		}

		private void SetUI(string loc, string status, string p, string s, Action onP, Action onS, Action onM = null)
		{
			locationText.text = loc;
			statusText.text = status;
			primaryBtn.GetComponentInChildren<Text>().text = p;
			secondaryBtn.GetComponentInChildren<Text>().text = s;
			primaryBtn.onClick.RemoveAllListeners(); secondaryBtn.onClick.RemoveAllListeners(); mapBtn.onClick.RemoveAllListeners();
			if (onP != null) primaryBtn.onClick.AddListener(() => onP());
			if (onS != null) secondaryBtn.onClick.AddListener(() => onS());
			if (onM != null) mapBtn.onClick.AddListener(() => onM());
		}

		private void DrawIdle()
		{
			var loc = locations.First(l => l.id == player.currentLocationId);
			SetUI(
				$"Konum: {loc.displayName}",
				$"Para: ${player.cash} | Wanted: {player.wanted}\nAraç: {player.currentVehicle?.name}\nAktif görev: {(activeMission == null ? "Yok" : activeMission.type.ToString())}",
				"Menü", activeMission == null ? "Görevler" : "Görev",
				OpenMenu, ShowMissions, ShowTravel
			);
		}

		private int menuIndex = 0;
		private readonly string[] mainMenu = new[] { "Seyahat", "Görevler", "Garaj", "Market", "İhale", "Durum" };
		private void OpenMenu()
		{
			menuIndex = (menuIndex + 1) % mainMenu.Length;
			var current = mainMenu[menuIndex];
			SetUI("Menü", string.Join(" | ", mainMenu.Select((m, i) => i == menuIndex ? $"[{m}]" : m)), "Seç", "İleri",
				() =>
				{
					switch (current)
					{
						case "Seyahat": ShowTravel(); break;
						case "Görevler": ShowMissions(); break;
						case "Garaj": ShowGarage(); break;
						case "Market": ShowMarket(); break;
						case "İhale": ShowAuction(); break;
						case "Durum": ShowStatus(); break;
					}
				},
				OpenMenu,
				DrawIdle
			);
		}

		private void ShowTravel()
		{
			var loc = locations.First(l => l.id == player.currentLocationId);
			var neighbors = loc.neighbors.Select(id => locations.First(l => l.id == id).displayName).ToList();
			string opts = neighbors.Count == 0 ? "Yakın yok" : string.Join(", ", neighbors);
			SetUI("Nereye?", $"Yakınlar: {opts}", neighbors.Count > 0 ? "Rastgele Git" : "Geri", activeMission != null ? "Hedefe Git" : "Geri",
				() =>
				{
					if (neighbors.Count == 0) { DrawIdle(); return; }
					var target = loc.neighbors[UnityEngine.Random.Range(0, loc.neighbors.Count)];
					TravelTo(target);
				},
				() => { if (activeMission == null) DrawIdle(); else TravelTo(activeMission.toLocationId); },
				DrawIdle
			);
		}

		private void TravelTo(string targetId)
		{
			player.currentLocationId = targetId;
			var endDistrict = CurrentDistrict();

			if (encounter.TryPoliceRaid(player.wanted))
			{
				SetUI("Polis Baskını", "Teslim ol ya da kaç.", "Teslim Ol", "Kaç",
					() => { player.cash = Mathf.Max(0, player.cash - 300); player.wanted = Mathf.Max(0, player.wanted - 25); DrawIdle(); },
					() =>
					{
						float chance = 0.2f + (player.currentVehicle?.stealth ?? 0f) * 0.6f;
						if (UnityEngine.Random.value < chance) player.wanted = Mathf.Min(100, player.wanted + 10);
						else { player.cash = Mathf.Max(0, player.cash - 500); player.wanted = Mathf.Min(100, player.wanted + 25); }
						DrawIdle();
					},
					DrawIdle
				);
				return;
			}

			if (encounter.TryGenerateEncounter(endDistrict.danger, player.wanted, out var type)) ResolveEncounter(type);
			else DrawIdle();
		}

		private void ResolveEncounter(EncounterType type)
		{
			switch (type)
			{
				case EncounterType.PoliceCheck:
					SetUI("Polis Kontrolü", "Belgeleri göster veya kaç.", "Belgeleri Göster", "Kaç",
						() => { if (UnityEngine.Random.value < 0.7f) player.wanted = Mathf.Max(0, player.wanted - 5); else { player.cash = Mathf.Max(0, player.cash - 100); player.wanted += 5; } DrawIdle(); },
						() => { float c = 0.3f + (player.currentVehicle?.stealth ?? 0f) * 0.5f; if (UnityEngine.Random.value < c) player.wanted = Mathf.Min(100, player.wanted + 5); else { player.cash = Mathf.Max(0, player.cash - 150); player.wanted = Mathf.Min(100, player.wanted + 15); } DrawIdle(); },
						DrawIdle
					);
					break;
				case EncounterType.GangAmbush:
					SetUI("Çete Pusu", "Parayı ver veya kaç.", "$200 Öde", "Kaç",
						() => { player.cash = Mathf.Max(0, player.cash - 200); DrawIdle(); },
						() => { if (UnityEngine.Random.value < 0.5f) player.wanted = Mathf.Min(100, player.wanted + 5); else player.cash = Mathf.Max(0, player.cash - 250); DrawIdle(); },
						DrawIdle
					);
					break;
				case EncounterType.StreetDeal:
					SetUI("Sokak Pazarlığı", "Küçük iş fırsatı.", "+$150 (Risk)", "Reddet",
						() => { player.cash += 150; player.wanted = Mathf.Min(100, player.wanted + 10); DrawIdle(); },
						DrawIdle,
						DrawIdle
					);
					break;
				case EncounterType.FoundCash:
					SetUI("Buluntu", "Yerde zarf buldun: $100", "Al", "Bırak",
						() => { player.cash += 100; DrawIdle(); },
						DrawIdle,
						DrawIdle
					);
					break;
			}
		}

		private void ShowMissions()
		{
			if (activeMission == null)
			{
				var lines = availableMissions.Select(m => $"{m.type}: {NameOf(m.fromLocationId)} -> {NameOf(m.toLocationId)} (${m.reward})").ToList();
				SetUI("Görevler", lines.Count == 0 ? "Görev yok." : string.Join("\n", lines), lines.Count > 0 ? "Rastgele Al" : "Geri", "Geri",
					() => { if (availableMissions.Count == 0) { DrawIdle(); return; } activeMission = availableMissions[UnityEngine.Random.Range(0, availableMissions.Count)]; activeMission.accepted = true; DrawIdle(); },
					DrawIdle,
					DrawIdle
				);
			}
			else
			{
				bool atPickup = player.currentLocationId == activeMission.fromLocationId;
				bool atDrop = player.currentLocationId == activeMission.toLocationId;
				switch (activeMission.type)
				{
					case MissionType.Delivery:
						if (!activeMission.completed && atPickup)
						{
							SetUI("Teslimat", "Paket alındı. Hedefe git.", "Hedefe Git", "İptal", () => TravelTo(activeMission.toLocationId), CancelMission, DrawIdle);
						}
						else if (!activeMission.completed && atDrop)
						{
							SetUI("Teslimat", $"Ödül: ${activeMission.reward}", "Teslim Et", "İptal",
								() => { player.cash += activeMission.reward; player.wanted = Mathf.Clamp(player.wanted + Mathf.RoundToInt(activeMission.risk * 0.2f), 0, 100); FinishMission(); },
								CancelMission, DrawIdle);
						}
						else
						{
							SetUI("Teslimat", "Hedefe ilerle.", "Hedefe Git", "İptal", () => TravelTo(activeMission.toLocationId), CancelMission, DrawIdle);
						}
						break;

					case MissionType.Heist:
						if (!activeMission.completed && atDrop)
						{
							SetUI("Soygun", "Yöntem seç.", "Sessiz", "Agresif",
								() => { bool ok = UnityEngine.Random.value < 0.55f * (1f + PowerMath.Mult(player.personalPower)); if (ok) { player.cash += activeMission.reward; player.wanted = Mathf.Min(100, player.wanted + 20); FinishMission(); } else { player.cash = Mathf.Max(0, player.cash - 200); player.wanted = Mathf.Min(100, player.wanted + 30); DrawIdle(); } },
								() => { bool ok = UnityEngine.Random.value < 0.7f * (1f + PowerMath.Mult(player.personalPower)); if (ok) { player.cash += Mathf.RoundToInt(activeMission.reward * 1.2f); player.wanted = Mathf.Min(100, player.wanted + 35); FinishMission(); } else { player.cash = Mathf.Max(0, player.cash - 400); player.wanted = Mathf.Min(100, player.wanted + 45); DrawIdle(); } },
								DrawIdle
							);
						}
						else SetUI("Soygun", "Hedefe git.", "Hedefe Git", "İptal", () => TravelTo(activeMission.toLocationId), CancelMission, DrawIdle);
						break;

					case MissionType.Tail:
						SetUI("Takip", "Mesafeyi koru.", "Yakın Takip", "Uzak Takip",
							() => { if (UnityEngine.Random.value < 0.7f * (1f + PowerMath.Mult(player.personalPower))) CompleteMission(); else { player.wanted = Mathf.Min(100, player.wanted + 10); DrawIdle(); } },
							() => { if (UnityEngine.Random.value < 0.45f * (1f + PowerMath.Mult(player.personalPower))) CompleteMission(); else DrawIdle(); },
							DrawIdle
						);
						break;
				}
			}
		}

		private void CancelMission() { activeMission = null; DrawIdle(); }
		private void CompleteMission() { player.cash += activeMission.reward; player.wanted = Mathf.Clamp(player.wanted + Mathf.RoundToInt(activeMission.risk * 0.2f), 0, 100); FinishMission(); }
		private void FinishMission() { activeMission.completed = true; activeMission = null; RefreshMissions(); DrawIdle(); }
		private string NameOf(string locId) => locations.First(l => l.id == locId).displayName;

		private void ShowGarage()
		{
			SetUI("Garaj", "Aracını yükselt.", "Yükselt (+Hız,+Giz)", "Geri",
				() =>
				{
					if (player.cash >= 300) { player.cash -= 300; player.currentVehicle.speed += 0.2f; player.currentVehicle.stealth = Mathf.Clamp01(player.currentVehicle.stealth + 0.05f); }
					DrawIdle();
				},
				DrawIdle,
				DrawIdle
			);
		}

		private void ShowMarket()
		{
			SetUI("Araç Pazarı", "Sedan $1200 | Sport $3200", "Sedan Al", "Sport Al",
				() => { if (player.cash >= 1200) { player.cash -= 1200; player.currentVehicle = new Vehicle { id = "sedan", name = "Sedan", price = 1200, speed = 1.2f, stealth = 0.3f }; } DrawIdle(); },
				() => { if (player.cash >= 3200) { player.cash -= 3200; player.currentVehicle = new Vehicle { id = "sport", name = "Sport", price = 3200, speed = 1.9f, stealth = 0.4f }; } DrawIdle(); },
				DrawIdle
			);
		}

		private void ShowAuction()
		{
			SetUI("İhale", "Teklif ver ya da sabotaj dene.", "Teklif Ver (-$200)", "Sabotaj (+Wanted)",
				() => { if (player.cash >= 200) player.cash -= 200; DrawIdle(); },
				() => { if (UnityEngine.Random.value < 0.5f * (1f + PowerMath.Mult(player.personalPower))) player.cash += 50; else player.wanted = Mathf.Min(100, player.wanted + 10); DrawIdle(); },
				DrawIdle
			);
		}

		private void ShowStatus()
		{
			SetUI("Durum", $"Para: ${player.cash}\nWanted: {player.wanted}\nKişisel Güç: {player.personalPower}", "PP Satın Al (R)", "Wanted Azalt ($250)",
				() => { /* R→PP akışı backend gerekir; burada stub bırakıyoruz */ player.personalPower += 5; DrawIdle(); },
				() => { if (player.cash >= 250) { player.cash -= 250; player.wanted = Mathf.Max(0, player.wanted - 15); } DrawIdle(); },
				DrawIdle
			);
		}
	}
}
