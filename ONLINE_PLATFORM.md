# Naval Command – Online-Plattform

## Umgesetzter Umfang

- verpflichtende Anmeldung über Unity Player Accounts; das gehostete Anmeldefenster unterstützt E-Mail sowie – nach Dashboard-Konfiguration – Apple und Google
- servergespeicherte Profile mit Anzeigename, Freundescode, Saison, MMR und Statistik
- Freundesliste, Anfragen, Blockieren und ungewertete Freundschaftsduelle
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

1. Authentication aktivieren und Unity Player Accounts als Identity Provider konfigurieren. Im Player-Accounts-Projekt E-Mail, Sign in with Apple und Sign in with Google aktivieren.
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

Aktueller Deployment-Status: Das Editor-Environment ist `development`
(`16a5256a-9928-41a3-a06f-d90941c49f80`). Der zuletzt dokumentierte Stand enthielt
11 deployte Ressourcen. Für den Arjan-Kauf müssen das aktualisierte Cloud-Code-Modul
und `COMMANDER_ARJAN_DHILLON` zusätzlich neu deployt werden.
Für die Imani-Werbefreischaltung muss das aktualisierte Cloud-Code-Modul ebenfalls neu
in `development` und später in `production` deployt werden.
Für Belohnungscodes muss das Cloud-Code-Modul mit der Funktion `RedeemRewardCode`
ebenfalls neu deployt werden; der Client allein kann die Freischaltung nicht vergeben.

## Mobile Builddaten

- Produktname: `Naval Command`
- Bundle/Application ID: `com.hasengschwandtner.navalcommand`
- Ausrichtung: Hochformat; Landscape und Upside-down sind deaktiviert
- App-Store-Altersbewertung und Datenschutzerklärung müssen vor Veröffentlichung die Online-Konten, Freundesdaten, Käufe und Unity-Dienste abdecken.

## Release-Gates

- Zwei echte Testkonten können sich anmelden, gegenseitig hinzufügen und ein Freundschaftsspiel abschließen.
- Zwei Geräte finden sich im Ranked-Matchmaking; Zugwechsel, Timeout, App-Neustart und Wiederverbindung funktionieren.
- Manipulierte Koordinaten, falsche Flotten, veraltete Versionen, doppelte Aktions-IDs und fremde Match-IDs werden abgelehnt.
- Apple Sandbox und Google License Testing bestätigen, dass ein Kauf erst nach Servervalidierung freigeschaltet wird und nach Neuinstallation wiederherstellbar ist.
- Saisonrangliste und MMR werden nach Sieg, Niederlage, Aufgabe und Timeout genau einmal aktualisiert.
- Die technische Kontolöschung ist getestet; Datenschutzerklärung, Supportweg, Jugendschutztexte und Store-Metadaten sind freigegeben.
