# Naval Command – Online-Plattform

## Umgesetzter Umfang

- verpflichtende Anmeldung über Unity Player Accounts; das gehostete Anmeldefenster unterstützt E-Mail sowie – nach Dashboard-Konfiguration – Apple und Google
- servergespeicherte Profile mit Anzeigename, Freundescode, Saison, MMR und Statistik
- Freundesliste, Anfragen, Blockieren und ungewertete Freundschaftsduelle
- Profil und Freunde führen ohne Anmeldung direkt zum Login; danach wird der gewählte Bereich geöffnet.
- Freundescode kopieren, Einladungen ablehnen und gesendete Duelle serverseitig abbrechen
- Freundesliste und Einladungen werden alle sechs Sekunden, aktive Spiele alle zwei Sekunden aktualisiert; Pushmeldungen beschleunigen diese Abrufe.
- Einladung und Duell verwenden dieselbe ID. Wiederholte Annahme liefert dieselbe Partie; Annahme und Abbruch konkurrieren auf demselben Cloud-Save-Write-Lock.
- Wiederverbindung stellt die eigenen Schiffspositionen und verbleibenden Bonusschüsse aus dem Serverzustand wieder her.
- zufälliges Ranked-Matchmaking mit wachsendem MMR-Suchfenster
- serverautoritatives 10×10-Match mit versteckten Flotten, Versionssperren und idempotenten Aktions-IDs
- 45-Sekunden-Zuglimit; eine Zeitüberschreitung verliert das Spiel, Verbindungsabbrüche pausieren die Uhr nicht
- Wiederverbindung über `activeMatchId` und Cloud-Code-Pushmeldungen
- Matchmaking-Polling und Wiederverbindung bei App-Rückkehr als Fallback für verlorene Pushmeldungen
- 8-Wochen-Saisons, fünf Platzierungsspiele, Elo-artige Rangpunkte und saisonale Ranglisten
- servergeprüfte Apple-/Google-Käufe; Unity IAP bestätigt Bestellungen erst nach erfolgreicher Cloud-Code-Prüfung
- Rewarded Ad für Kapitän Imani Cross über Unity LevelPlay; eine vollständig abgeschlossene Anzeige schaltet sie dauerhaft im Konto frei
- servergespeicherte Belohnungscodes im Shop; `op_start` schaltet einmalig alle vorhandenen Kommandanten für das Konto frei
- Altersfreigabe im Login (16+), keine Freitext-Kommunikation und keine vertraulichen Gegnerdaten im Client
- doppelt bestätigte Kontolöschung; laufende Matches werden dabei serverseitig aufgegeben und Profildaten entfernt

## Rangsystem

| Liga | MMR |
|---|---:|
| Rekrut | unter 900 |
| Bronze | 900–1049 |
| Silber | 1050–1199 |
| Gold | 1200–1349 |
| Platin | 1350–1499 |
| Admiral | ab 1500 |

Neue Konten starten mit 1000 MMR. Die ersten fünf Matches nutzen K=48, danach K=24. Beim Saisonwechsel wird der Abstand zu 1000 halbiert und die Platzierung beginnt neu.

## Serverseitige Sicherheitsgrenzen

- Der Client übermittelt nur Absicht: Zielzelle, Fähigkeit, Matchversion und eindeutige Aktions-ID.
- Flotten, Treffer, Punkte, Minen, Zugfolge, Gewinner und Rangpunkte werden ausschließlich in Cloud Code berechnet.
- Gegneransichten enthalten nur bereits bekannte Treffer, Fehlschüsse und regelkonform aufgedeckte Kontakte.
- Cloud-Save-Datensätze sind private Custom Items und werden mit Write Locks aktualisiert.
- Rangbelohnungen und Käufe besitzen serverseitige Idempotenzmarker.
- Freundschaftsmatches verändern weder MMR noch Ranglisten.
- Der lokale Debugmodus wird in Online-Partien ignoriert.

## Einmalige Dashboard-Konfiguration

Das Unity-Projekt ist mit Cloud Project ID `5f667b43-665f-488b-90ea-34f645b98099` verbunden. Vor einem echten Gerätetest müssen im Unity Dashboard im Environment `development` folgende Ressourcen angelegt werden:

1. Authentication aktivieren. Unity Player Accounts für native Builds und `Username and Password` für WebGL als Identity Provider konfigurieren. Im Player-Accounts-Projekt E-Mail, Sign in with Apple und Sign in with Google aktivieren.
2. Friends, Cloud Save, Cloud Code, Economy und Leaderboards für das Environment aktivieren.
3. Im Deployment-Fenster alle 12 vorhandenen Ressourcen deployen: das Modul aus `Assets/CloudCode`, acht Saison-Ranglisten aus `Assets/Leaderboards` und drei Economy-Kaufdefinitionen aus `Assets/Economy`.
4. Die Ranglisten sind als `desc` + `keepLatest` definiert, damit eine MMR-Niederlage den Rang korrekt senkt. Vor Saison 09 weitere Dateien nach demselben Schema ergänzen.
5. Die Economy-Ressourcen heißen `COMMANDER_ELIAS_VOSS`, `COMMANDER_DAE_HYUN_KWON` und `COMMANDER_ARJAN_DHILLON`; die Arjan-Store-ID lautet `commander.arjan.dhillon`. Für Arjan muss in App Store Connect und Google Play ein Preis von 2,99 € beziehungsweise die entsprechende lokale Preisstufe eingestellt werden.
6. In App Store Connect und Google Play Console dieselben Non-Consumable-Produkte konfigurieren. Store-Verträge, Steuer-/Bankdaten und Testkonten müssen aktiv sein.
7. Apple- und Google-Belegprüfung in Economy vollständig konfigurieren. Keine Store-Schlüssel oder Service-Account-Dateien in `Assets` ablegen.
8. Für Produktion ein eigenes UGS-Environment erstellen, die Ressourcen dorthin kopieren und den Client-Build auf `production` umstellen.
9. LevelPlay verwendet für Android App Key `27be23ebd` und Rewarded Ad Unit ID `ha35o9hutwffhnws`. In `Setup > Instances` muss für diese Ad Unit mindestens Unity Ads oder ironSource Ads aktiv sein. Die Unity-Ads-Adapterabhängigkeit wird vom LevelPlay-Paket mitinstalliert.
10. Der aktuelle Entwicklungsfluss ruft `ClaimImaniRewardedAd` nach dem Client-Callback `OnAdRewarded` auf. Vor Produktion muss dieser Grant durch einen öffentlichen LevelPlay-S2S-Callback mit Event-ID-Prüfung ersetzt werden; andernfalls könnte ein manipulierter Client die Funktion direkt aufrufen.

Der Editor verwendet standardmäßig `development`. Produktions-Builds müssen das Scripting Define Symbol `NAVAL_PRODUCTION` setzen; dadurch wählt `NavalOnlineEnvironment.Current` ausschließlich das Environment `production`.

Deployment am 04.09.2026: `NavalCommandOnline.ccmr` wurde über die Unity-Deployment-API
erfolgreich nach `development` veröffentlicht (`Up to date`, Fortschritt 100 %).
Das Modul enthält auch `PollFriendlyMatch`, `CancelFriendlyMatch`, `DeclineFriendlyMatch`
und die Wiederherstellung der eigenen Flotte. Protokoll: `TestResults/online-deployment.txt`.
Die Economy- und Ranglistenressourcen wurden in diesem Durchlauf nicht verändert.

Das Environment hat die ID `16a5256a-9928-41a3-a06f-d90941c49f80`.
Die früher dokumentierte fehlende Economy-Definition `COMMANDER_ARJAN_DHILLON`
muss vor einem Store-Release separat geprüft werden. Reward-Code- und Imani-Funktionen
sind im aktualisierten Modul enthalten; Käufe und Werbung wurden hier nicht live getestet.

## Mobile Builddaten

- Produktname: `Naval Command`
- Bundle/Application ID: `com.hasengschwandtner.navalcommand`
- Ausrichtung: Hochformat; Landscape und Upside-down sind deaktiviert
- App-Store-Altersbewertung und Datenschutzerklärung müssen vor Veröffentlichung die Online-Konten, Freundesdaten, Käufe und Unity-Dienste abdecken.

## Plattformgrenze und Verifikation

Das installierte Player-Accounts-SDK implementiert den Browser-Login für Editor,
Windows, Android und iOS, aber nicht für WebGL (`BrowserUtils.CreateBrowserUtils`
liefert dort `null`). WebGL verwendet deshalb die plattformunabhängige Unity-
Authentication-Anmeldung mit Nutzername und Passwort. Der passende Identity Provider
ist im Environment `development` aktiviert; native Builds verwenden weiterhin Player
Accounts.

Unity-Regressionen laufen vollständig im EditMode. Zusätzlich prüfen die Backend-Tests
zwei simulierte Spieler, Annahme/Abbruch gleichzeitig, abgelehnte Einladungen,
verlorene Antworten, verborgene Gegnerflotten, Zugwechsel, Wiederverbindung und
einmalige Freundschaftsspiel-Ergebnisse:

```powershell
dotnet test Backend/NavalCommand.CloudCode.Tests/NavalCommand.CloudCode.Tests.csproj -c Release
```

Der Testhost nutzt das installierte .NET 10; das Cloud-Code-Modul bleibt auf .NET 9.
Diese Tests simulieren Cloud Save und die Freundschaftsprüfung. Der unten aufgeführte
Test mit zwei echten angemeldeten Konten bleibt ein eigener Release-Gate.

Prüfstand 05.09.2026: 72/72 Unity-EditMode-Tests und 7/7 Backend-Tests bestanden.
Ergebnisse liegen in `TestResults/unity-editmode.xml` und
`Backend/NavalCommand.CloudCode.Tests/TestResults/friendly-multiplayer.trx`.
Der Windows-Development-Build wurde über Unity MCP erfolgreich gebaut (0 Fehler,
eine Warnung wegen einer `System.Windows.Forms`-Referenz) und im isolierten
Starttest ohne Laufzeitausnahmen gestartet. Das ZIP liegt unter
`Builds/NavalCommand-Windows-Online.zip`. Login, Profil und Freunde wurden visuell
im Editor geprüft; die Profil-/Freundevorschau verwendete einen Testdienst.
Das aktive Buildziel wurde für den neuen Hochformat-Build auf Windows umgestellt.

Zusätzlich bestanden Live-Smoke-Tests gegen `development`: Registrierung, erneute
Anmeldung, Profilerstellung über das veröffentlichte Cloud-Code-Modul sowie eine echte
Ranglistenpartie mit zwei temporären Konten, übereinstimmenden Spieleransichten und
kontrolliertem Matchende. Die Testkonten wurden danach gelöscht. Der WebGL-Build wurde
auf GitHub Pages veröffentlicht; der Windows-Build in `windos build 3` ist auf ein
nicht skalierbares 720×1280-Fenster ohne Vollbildwechsel festgelegt.

## Release-Gates

- Zwei echte Testkonten können sich anmelden, gegenseitig hinzufügen und ein Freundschaftsspiel abschließen.
- Zwei Geräte finden sich im Ranked-Matchmaking; Zugwechsel, Timeout, App-Neustart und Wiederverbindung funktionieren.
- Manipulierte Koordinaten, falsche Flotten, veraltete Versionen, doppelte Aktions-IDs und fremde Match-IDs werden abgelehnt.
- Apple Sandbox und Google License Testing bestätigen, dass ein Kauf erst nach Servervalidierung freigeschaltet wird und nach Neuinstallation wiederherstellbar ist.
- Saisonrangliste und MMR werden nach Sieg, Niederlage, Aufgabe und Timeout genau einmal aktualisiert.
- Die technische Kontolöschung ist getestet; Datenschutzerklärung, Supportweg, Jugendschutztexte und Store-Metadaten sind freigegeben.
