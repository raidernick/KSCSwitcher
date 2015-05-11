using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using KSP;
using KSP.IO;

/******************************************************************************
 * Copyright (c) 2014~2015, Justin Bengtson
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met: 
 * 
 * 1. Redistributions of source code must retain the above copyright notice,
 * this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 * this list of conditions and the following disclaimer in the documentation
 * and/or other materials provided with the distribution.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 ******************************************************************************/

namespace regexKSP {
	[KSPAddon(KSPAddon.Startup.TrackingStation, false)]
	public class KSCSwitcher : MonoBehaviour {
		public SortedList<string, LaunchSite> siteLocations;
		public string activeSite;
		private bool showWindow;
		private bool showSites = true;
		private bool oldButton = false;
		private string curTooltip = "";
		private Vector2 scrollPosition;
		private Texture2D lsTexture;
		private Texture2D lsActiveTexture;
		private Texture2D lsButtonNormal;
		private Texture2D lsButtonHighlight;
		private Texture2D eyeButtonNormal;
		private Texture2D eyeButtonHighlight;
		private Texture2D magButtonNormal;
		private GUIStyle bStyle = null;
		private GUIStyle siteText = null;
		private GUIStyle infoLabel = null;

		public void Start() {
            showWindow = false;
			scrollPosition = Vector2.zero;
			siteLocations = KSCLoader.instance.Sites.getSitesGeographicalList();
			if(KSCLoader.instance.Sites.lastSite.Length > 0) {
				activeSite = KSCLoader.instance.Sites.lastSite;
	            print("KSCSwitcher set the active site to the last site of " + activeSite);
			} else if(KSCLoader.instance.Sites.defaultSite.Length > 0) {
				activeSite = KSCLoader.instance.Sites.defaultSite;
	            print("KSCSwitcher set the active site to the default site of " + activeSite);
			} else {
	            print("KSCSwitcher could not set the active site");
			}
			loadTextures();
			RenderingManager.AddToPostDrawQueue(2, this.onDraw);
			RenderingManager.AddToPostDrawQueue(3, this.onDrawGUI);
            print("KSCSwitcher initialized");
		}
		
		public void OnDestroy() {
			RenderingManager.RemoveFromPostDrawQueue(2, this.onDraw);
			RenderingManager.RemoveFromPostDrawQueue(3, this.onDrawGUI);
		}
		
		public void onDrawGUI() {
			if(siteLocations.Count < 1) { return; }
			
			GUI.skin = HighLogic.Skin;
			if(bStyle == null) {
				bStyle = new GUIStyle(GUI.skin.button);
				bStyle.padding = new RectOffset();
				bStyle.contentOffset = new Vector2();
			}
			if(oldButton) {
				if(GUI.Button(new Rect(Screen.width - 100, 45, 100, 30), "Launch Sites")) {
					showWindow = !showWindow;
				}
			} else {
				if(GUI.Button(new Rect(Screen.width - 33, 45, 28, 28), (showWindow ? lsButtonHighlight : lsButtonNormal), bStyle)) {
					showWindow = !showWindow;
				}
			}
			if(GUI.Button(new Rect(Screen.width - 33, 78, 28, 28), (showSites ? eyeButtonHighlight : eyeButtonNormal), bStyle)) {
				showSites = !showSites;
			}
			if(showWindow) {
				if(oldButton) {
					GUILayout.BeginArea(new Rect(Screen.width - 333, 75, 300, 400));
				} else {
					GUILayout.BeginArea(new Rect(Screen.width - 333, 45, 300, 400));
				}
				scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(300), GUILayout.Height(400));
				Color defColor = GUI.color;
				bool isActiveSite = false;
				foreach(KeyValuePair<string, LaunchSite> kvp in siteLocations) {
					isActiveSite = kvp.Value.name.Equals(activeSite);
					GUILayout.BeginHorizontal();
					if(GUILayout.Button(magButtonNormal, bStyle, GUILayout.MaxWidth(28))) {
						focusOnSite(kvp.Value.geographicLocation);
					}
					if(isActiveSite) {
						GUI.contentColor = XKCDColors.ElectricLime;
					}
					if(GUILayout.Button(new GUIContent(kvp.Value.displayName, kvp.Value.description))) {
						if(isActiveSite) {
							ScreenMessages.PostScreenMessage("Cannot set launch site to active site.", 2.5f, ScreenMessageStyle.LOWER_CENTER);
						} else {
							setSite(kvp.Value);
						}
					}
					GUI.contentColor = defColor;
					GUILayout.EndHorizontal();
				}
				GUILayout.EndScrollView();
				GUILayout.EndArea();
				
				GUI.backgroundColor = XKCDColors.AlmostBlack;
				if(curTooltip != "") {
					if(oldButton) {
						GUI.Label(new Rect(Screen.width - 633, 75, 300, 400), GUI.tooltip, infoLabel);
					} else {
						GUI.Label(new Rect(Screen.width - 633, 45, 300, 400), GUI.tooltip, infoLabel);
					}
				}
				if(Event.current.type == EventType.Repaint) {
					curTooltip = GUI.tooltip;
				}
			}
		}

		public void onDraw() {
			if(siteLocations.Count < 1 || lsTexture == null || !showSites || !iconDisplayDistance()) { return; }

			CelestialBody Kerbin = getKSCBody();
			bool isActiveSite = false;
			foreach(KeyValuePair<string, LaunchSite> kvp in siteLocations) {
				Camera camera = MapView.MapCamera.camera;
				Vector3d point = Kerbin.GetWorldSurfacePosition(kvp.Value.geographicLocation.x, kvp.Value.geographicLocation.y, 0);
				if(!IsOccluded(point, Kerbin)) {
					isActiveSite = kvp.Value.name.Equals(activeSite);
					point = ScaledSpace.LocalToScaledSpace(point);
					point = camera.WorldToScreenPoint(point);
					Rect iconBound = new Rect((float) point.x, (float) (Screen.height - point.y), 28f, 28f);
					if(isActiveSite) {
						Graphics.DrawTexture(iconBound, lsActiveTexture);
					} else {
						Graphics.DrawTexture(iconBound, lsTexture);
					}

					if(iconBound.Contains(Event.current.mousePosition)) {
                        GUI.Label(new Rect((float)(point.x) + 28f, (float)(Screen.height - point.y) + 5f, 50, 20), kvp.Value.displayName, siteText);
						if(Event.current.type == EventType.mouseDown && Event.current.button == 0) {
							if(isActiveSite) {
								ScreenMessages.PostScreenMessage("Cannot set launch site to active site.", 2.5f, ScreenMessageStyle.LOWER_CENTER);
							} else {
								setSite(kvp.Value);
							}
						}
					}
				}
			}
		}
		
		public static CelestialBody getKSCBody() {
			CelestialBody Kerbin = FlightGlobals.Bodies.Find(body => body.name == "Kerbin");
            if(Kerbin == null) {
                Kerbin = FlightGlobals.Bodies.Find(body => body.name == "Earth"); // temp fix
            }
			return Kerbin;
		}
		
		public static bool setSite(ConfigNode KSC) {
			bool hasChanged = false;
            double dtmp;
            float ftmp;
            bool btmp;

			ConfigNode pqsCity = KSC.GetNode("PQSCity");
			if(pqsCity == null) { return false; }
			ConfigNode pqsDecal = KSC.GetNode("PQSMod_MapDecalTangent");

			CelestialBody Kerbin = getKSCBody();
			var mods = Kerbin.pqsController.transform.GetComponentsInChildren(typeof(PQSMod), true);

			foreach(var m in mods) {
                if(m.GetType().ToString().Equals("PQSCity")) {
                    PQSCity mod = m as PQSCity;
                    if(pqsCity.HasValue("KEYname")) {
                        if(!(mod.name.Equals(pqsCity.GetValue("KEYname")))) {
                            continue;
                        }
                    }
                    if(pqsCity.HasValue("repositionRadial")) {
                        mod.repositionRadial = KSPUtil.ParseVector3(pqsCity.GetValue("repositionRadial"));
                    }
                    if(pqsCity.HasValue("latitude") && pqsCity.HasValue("longitude")) {
                        double lat, lon;
                        double.TryParse(pqsCity.GetValue("latitude"), out lat);
                        double.TryParse(pqsCity.GetValue("longitude"), out lon);
                    
                        mod.repositionRadial = KSCSwitcher.LLAtoECEF(lat, lon, 0, Kerbin.Radius);
                    }
                    if(pqsCity.HasValue("reorientInitialUp")) {
                        mod.reorientInitialUp = KSPUtil.ParseVector3(pqsCity.GetValue("reorientInitialUp"));
                    }
                    if(pqsCity.HasValue("repositionToSphere")) {
                        if(bool.TryParse(pqsCity.GetValue("repositionToSphere"), out btmp)) {
                            mod.repositionToSphere = btmp;
                        }
                    }
                    if(pqsCity.HasValue("repositionToSphereSurface")) {
                        if(bool.TryParse(pqsCity.GetValue("repositionToSphereSurface"), out btmp)) {
                            mod.repositionToSphereSurface = btmp;
                        }
                    }
                    if (pqsCity.HasValue("repositionToSphereSurfaceAddHeight"))
                    {
                        if (bool.TryParse(pqsCity.GetValue("repositionToSphereSurfaceAddHeight"), out btmp))
                        {
                            mod.repositionToSphereSurfaceAddHeight = btmp;
                        }
                    }
                    if(pqsCity.HasValue("reorientToSphere")) {
                        if(bool.TryParse(pqsCity.GetValue("reorientToSphere"), out btmp)) {
                            mod.reorientToSphere = btmp;
                        }
                    }
                    if(pqsCity.HasValue("repositionRadiusOffset")) {
                        if(double.TryParse(pqsCity.GetValue("repositionRadiusOffset"), out dtmp)) {
                            mod.repositionRadiusOffset = dtmp;
                        }
                    }
                    if(pqsCity.HasValue("lodvisibleRangeMult")) {
                        if(double.TryParse(pqsCity.GetValue("lodvisibleRangeMult"), out dtmp)) {
                            foreach(PQSCity.LODRange l in mod.lod) {
                                l.visibleRange *= (float)dtmp;
                            }
                        }
                    }
                    if(pqsCity.HasValue("reorientFinalAngle")) {
                        if(float.TryParse(pqsCity.GetValue("reorientFinalAngle"), out ftmp)) {
                            mod.reorientFinalAngle = ftmp;
                        }
                    }
                    print("KSCSwitcher changed PQSCity");
                    
                    hasChanged = true;
                    mod.OnSetup();
                    mod.OnPostSetup();
                    SpaceCenter.Instance.transform.localPosition = mod.transform.localPosition;
                    SpaceCenter.Instance.transform.localRotation = mod.transform.localRotation;
                }

                // KSC Flat area
                if(pqsDecal != null && m.GetType().ToString().Equals("PQSMod_MapDecalTangent")) {
                    // thanks to asmi for this!
                    PQSMod_MapDecalTangent mod = m as PQSMod_MapDecalTangent;
                    if(pqsDecal.HasValue("position")) {
                        mod.position = KSPUtil.ParseVector3(pqsDecal.GetValue("position"));
                    }
                    if(pqsDecal.HasValue("radius")) {
                        if(double.TryParse(pqsDecal.GetValue("radius"), out dtmp)) {
                            mod.radius = dtmp;
                        }
                    }
                    if(pqsDecal.HasValue("heightMapDeformity")) {
                        if(double.TryParse(pqsDecal.GetValue("heightMapDeformity"), out dtmp)) {
                            mod.heightMapDeformity = dtmp;
                        }
                    }
                    if(pqsDecal.HasValue("absoluteOffset")) {
                        if(double.TryParse(pqsDecal.GetValue("absoluteOffset"), out dtmp)) {
                            mod.absoluteOffset = dtmp;
                        }
                    }
                    if(pqsDecal.HasValue("absolute")) {
                        if(bool.TryParse(pqsDecal.GetValue("absolute"), out btmp)) {
                            mod.absolute = btmp;
                        }
                    }
                    if(pqsDecal.HasValue("latitude") && pqsDecal.HasValue("longitude")) {
                        double lat, lon;
                        double.TryParse(pqsDecal.GetValue("latitude"), out lat);
                        double.TryParse(pqsDecal.GetValue("longitude"), out lon);
                        
                        mod.position = KSCSwitcher.LLAtoECEF(lat, lon, 0, Kerbin.Radius);
                    }
                    print("KSCSwitcher changed MapDecal_Tangent");

                    hasChanged = true;
                    mod.OnSetup();
                }
			}

			if(hasChanged) {
				if(KSC.HasValue("name")) {
					KSCLoader.instance.Sites.lastSite = LastKSC.fetch.lastSite = KSC.GetValue("name");
                    print("KSCSwitcher changed MapDecal_Tangent");
				}
				// Kerbin.pqsController.RebuildSphere();
			}

			return hasChanged;
		}

		private void setSite(LaunchSite newSite) {
			ConfigNode site = KSCLoader.instance.Sites.getSiteByName(newSite.name);
            if(site == null) { return; }
			
			if(KSCSwitcher.setSite(site)) {
				activeSite = newSite.name;
				ScreenMessages.PostScreenMessage("Launch site changed to " + newSite.displayName, 2.5f, ScreenMessageStyle.LOWER_CENTER);
				showWindow = false;
                // KSCReset.shouldCameraBeReset = true;
                // print("KSCSwitcher Launch site updated.  Camera reset set to true");
            }
		}
		
		private void focusOnSite(Vector2d loc) {
			Debug.Log("Focusing on site");
			PlanetariumCamera camera = PlanetariumCamera.fetch;
			CelestialBody Kerbin = getKSCBody();
			Vector3d point = ScaledSpace.LocalToScaledSpace(Kerbin.GetWorldSurfacePosition(loc.x, loc.y, 0));
			Vector3 vec = ScaledSpace.LocalToScaledSpace(Kerbin.transform.localPosition);
			point = (point - vec).normalized * Kerbin.Radius;
			camera.SetCamCoordsFromPosition(new Vector3((float) point.x, (float) point.y, (float) point.z));
			
			// this works for RSS, may have to change for other sizes.
			// float distance = camera.startDistance * 3.5f;
			float distance = (float) (Kerbin.Radius * 0.00035);
			camera.SetDistance(distance);
		}
		
        private bool IsOccluded(Vector3d loc, CelestialBody body) {
            Vector3d camPos = ScaledSpace.ScaledToLocalSpace(PlanetariumCamera.Camera.transform.position);

            if(Vector3d.Angle(camPos - loc, body.position - loc) > 90) { return false; }
			return true;
        }
		
		private void loadTextures() {
			Texture2D white = null;
			// hard-coded texture path because why not.
			try {
				lsButtonNormal = GameDatabase.Instance.GetTexture("KSCSwitcher/Plugins/Icons/launch-site-normal", false);
				lsButtonHighlight = GameDatabase.Instance.GetTexture("KSCSwitcher/Plugins/Icons/launch-site-highlight", false);
				if(lsButtonNormal == null) {
					oldButton = true;
				}
				lsTexture = GameDatabase.Instance.GetTexture("KSCSwitcher/Plugins/Icons/launch-site-texture", false);
				lsActiveTexture = GameDatabase.Instance.GetTexture("KSCSwitcher/Plugins/Icons/launch-site-active-texture", false);
				white = GameDatabase.Instance.GetTexture("KSCSwitcher/Plugins/Icons/info-background", false);
				eyeButtonNormal = GameDatabase.Instance.GetTexture("KSCSwitcher/Plugins/Icons/eye-normal", false);
				eyeButtonHighlight = GameDatabase.Instance.GetTexture("KSCSwitcher/Plugins/Icons/eye-highlight", false);
				magButtonNormal = GameDatabase.Instance.GetTexture("KSCSwitcher/Plugins/Icons/magnifier-normal", false);
			} catch(Exception e) {
				Debug.Log("Could not load button textures for KSCSwitcher, reverting to old button style: " + e.StackTrace);
				oldButton = true;
			}

			siteText = new GUIStyle();
			siteText.padding = new RectOffset(0, 0, 0, 0);
			siteText.stretchWidth = true;
			siteText.margin = new RectOffset(0, 0, 0, 0);
			siteText.alignment = TextAnchor.MiddleLeft;
			siteText.fontStyle = FontStyle.Bold;
			siteText.normal.textColor = XKCDColors.BrightOrange;

			infoLabel = new GUIStyle();
			infoLabel.padding = new RectOffset(5, 5, 5, 5);
			infoLabel.stretchHeight = true;
			infoLabel.margin = new RectOffset(0, 0, 0, 0);
			infoLabel.alignment = TextAnchor.UpperLeft;
			infoLabel.fontStyle = FontStyle.Bold;
			infoLabel.normal.textColor = XKCDColors.ElectricLime;
			infoLabel.normal.background = white;
			infoLabel.richText = true;
			infoLabel.wordWrap = true;
		}
		
		private bool iconDisplayDistance() {
			CelestialBody Kerbin = getKSCBody();
			return MapView.MapCamera.Distance < 25000 && MapView.MapCamera.target.name == Kerbin.name;
		}

        private static Vector3 LLAtoECEF(double lat, double lon, double alt, double radius) {
            const double degreesToRadians = Math.PI / 180.0;
            lat = (lat - 90) * degreesToRadians;
            lon *= degreesToRadians;
            double x, y, z;
            double n = radius; // for now, it's still a sphere, so just the radius
            x = (n + alt) * -1.0 * Math.Sin(lat) * Math.Cos(lon);
            y = (n + alt) * Math.Cos(lat); // for now, it's still a sphere, so no eccentricity
            z = (n + alt) * -1.0 * Math.Sin(lat) * Math.Sin(lon);
            return new Vector3((float)x, (float)y, (float)z);
        }
    }
}