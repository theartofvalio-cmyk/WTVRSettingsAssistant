namespace WTVRSettingsAssistant;

internal static partial class AppText
{
    // Shared additions are grouped by language so new UI text never depends
    // on the legacy partial catalogs or their English fallback copies.
    private static readonly Dictionary<string, Dictionary<string, string>> SupplementalTexts = new()
    {
        ["en"] = InfoPack(new(),
            "Works automatically with SteamVR, OpenXR and VDXR.", "Visual theme",
            "Your control center for War Thunder VR: switch Monitor/VR setups, manage aircraft profiles, configure Neck Assistant and KeyBind Assistant, and use VTrim for HOTAS trim and axis shaping.",
            "PATCH NOTES  ·  v1.0 → v{0}",
            "v1.0  ·  CORE VR ASSISTANT\n• Monitor/VR graphics switching and War Thunder launcher integration.\n• Neck Assistant for extended rear visibility in VR.\n• Saved graphics/control setups and basic profile management.\n\nv1.1  ·  KEYBINDS & QUALITY OF LIFE\n• KeyBind Assistant for hidden War Thunder commands using keyboard, mouse or HOTAS.\n• Physical ON/OFF switch support, persistent assistant states and safer app updating.\n• Improved OpenXR/SteamVR/VDXR handling and UI scaling.\n\nv2.0  ·  VTRIM & AIRCRAFT PROFILES\n• Embedded VTrim with vJoy output, physical-axis routing and virtual trim.\n• Automatic aircraft detection with aircraft-specific Custom Profiles.\n• Per-aircraft trim bindings, response curves, copy/paste tools and cached aircraft artwork.\n\nv2.0.1  ·  CURRENT RELEASE\n• Reworked aircraft profiles with full-page editing and custom curve points.\n• Improved VTrim navigation, device/output setup, rendering and responsive layouts.\n• Expanded localization plus launcher, update, profile-state and flicker fixes.\n• Flight Assistant remains disabled while official Gaijin clarification is pending.",
            "Free and open source community software. This unofficial tool is not affiliated with, endorsed by, or sponsored by Gaijin Entertainment."),

        ["bg"] = InfoPack(BulgarianCorrections(),
            "Работи автоматично със SteamVR, OpenXR и VDXR.", "Визуална тема",
            "Твоят контролен център за War Thunder VR: превключване между Monitor/VR настройки, управление на самолетни профили, Neck Assistant и KeyBind Assistant, както и VTrim за HOTAS трим и настройка на осите.",
            "БЕЛЕЖКИ ПО ВЕРСИИТЕ  ·  v1.0 → v{0}",
            "v1.0  ·  ОСНОВЕН VR ASSISTANT\n• Превключване на графични Monitor/VR настройки и интеграция с War Thunder launcher-а.\n• Neck Assistant за по-удобен поглед назад във VR.\n• Запазени графични/контролни настройки и базово управление на профили.\n\nv1.1  ·  KEYBINDS И ПОДОБРЕНИЯ\n• KeyBind Assistant за скрити War Thunder команди чрез клавиатура, мишка или HOTAS.\n• Поддръжка на физически ON/OFF превключватели, запомняне на състоянията и по-безопасни обновявания.\n• Подобрена работа с OpenXR/SteamVR/VDXR и мащабиране на интерфейса.\n\nv2.0  ·  VTRIM И САМОЛЕТНИ ПРОФИЛИ\n• Вграден VTrim с vJoy output, маршрутизиране на физически оси и виртуален трим.\n• Автоматично разпознаване на самолета и отделни Custom Profiles.\n• Индивидуални trim bindings, response curves, copy/paste инструменти и кеширани изображения на самолетите.\n\nv2.0.1  ·  ТЕКУЩА ВЕРСИЯ\n• Преработени самолетни профили с редактиране на цяла страница и custom точки по кривите.\n• Подобрена VTrim навигация, Devices & Output настройка, рендериране и responsive layout.\n• Разширени преводи и множество поправки по launcher-а, update процеса, profile state-а и flicker-а.\n• Flight Assistant остава изключен, докато се чака официално уточнение от Gaijin.",
            "Безплатен community софтуер с отворен код. Този неофициален инструмент не е свързан, одобрен или спонсориран от Gaijin Entertainment."),

        ["es"] = InfoPack(new(),
            "Funciona automáticamente con SteamVR, OpenXR y VDXR.", "Tema visual",
            "Tu centro de control para War Thunder VR: cambia entre configuraciones Monitor/VR, gestiona perfiles de aeronaves, configura Neck Assistant y KeyBind Assistant y usa VTrim para trim HOTAS y curvas de ejes.",
            "NOTAS DE VERSIÓN  ·  v1.0 → v{0}",
            "v1.0  ·  ASISTENTE VR BÁSICO\n• Cambio de gráficos Monitor/VR e integración con el lanzador de War Thunder.\n• Neck Assistant para mejorar la visibilidad trasera en VR.\n• Configuraciones de gráficos/controles guardadas y gestión básica de perfiles.\n\nv1.1  ·  KEYBINDS Y MEJORAS\n• KeyBind Assistant para comandos ocultos de War Thunder mediante teclado, ratón o HOTAS.\n• Compatibilidad con interruptores físicos ON/OFF, estados persistentes y actualizaciones más seguras.\n• Mejor compatibilidad con OpenXR/SteamVR/VDXR y escalado de la interfaz.\n\nv2.0  ·  VTRIM Y PERFILES DE AERONAVES\n• VTrim integrado con salida vJoy, enrutado de ejes físicos y trim virtual.\n• Detección automática de aeronaves con perfiles personalizados por aeronave.\n• Bindings de trim, curvas de respuesta, copiar/pegar e imágenes de aeronaves almacenadas en caché.\n\nv2.0.1  ·  VERSIÓN ACTUAL\n• Perfiles de aeronaves rediseñados con edición de página completa y puntos personalizados en las curvas.\n• Mejor navegación de VTrim, configuración de dispositivos/salida, renderizado y diseño adaptable.\n• Más traducciones y correcciones del lanzador, actualizaciones, estado de perfiles y parpadeos.\n• Flight Assistant permanece desactivado mientras se espera una aclaración oficial de Gaijin.",
            "Software comunitario gratuito y de código abierto. Esta herramienta no oficial no está afiliada, respaldada ni patrocinada por Gaijin Entertainment."),

        ["de"] = InfoPack(new(),
            "Funktioniert automatisch mit SteamVR, OpenXR und VDXR.", "Visuelles Design",
            "Deine Zentrale für War Thunder VR: Monitor/VR-Setups wechseln, Flugzeugprofile verwalten, Neck Assistant und KeyBind Assistant konfigurieren und VTrim für HOTAS-Trimmung und Achsenkurven verwenden.",
            "PATCHNOTES  ·  v1.0 → v{0}",
            "v1.0  ·  VR-GRUNDASSISTENT\n• Umschalten von Monitor/VR-Grafikprofilen und Integration des War-Thunder-Launchers.\n• Neck Assistant für bessere Sicht nach hinten in VR.\n• Gespeicherte Grafik-/Steuerungsprofile und grundlegende Profilverwaltung.\n\nv1.1  ·  KEYBINDS & KOMFORT\n• KeyBind Assistant für versteckte War-Thunder-Befehle über Tastatur, Maus oder HOTAS.\n• Unterstützung physischer ON/OFF-Schalter, gespeicherte Assistenten-Zustände und sicherere Updates.\n• Verbesserte OpenXR/SteamVR/VDXR-Unterstützung und UI-Skalierung.\n\nv2.0  ·  VTRIM & FLUGZEUGPROFILE\n• Integriertes VTrim mit vJoy-Ausgabe, physischem Achsenrouting und virtueller Trimmung.\n• Automatische Flugzeugerkennung mit flugzeugspezifischen Custom Profiles.\n• Trim-Bindings, Reaktionskurven, Kopieren/Einfügen und zwischengespeicherte Flugzeugbilder pro Profil.\n\nv2.0.1  ·  AKTUELLE VERSION\n• Überarbeitete Flugzeugprofile mit Ganzseiten-Editor und frei platzierbaren Kurvenpunkten.\n• Verbesserte VTrim-Navigation, Geräte-/Ausgabeeinrichtung, Darstellung und responsive Layouts.\n• Erweiterte Übersetzungen sowie Korrekturen für Launcher, Updates, Profilstatus und Flackern.\n• Flight Assistant bleibt deaktiviert, bis eine offizielle Klarstellung von Gaijin vorliegt.",
            "Kostenlose Open-Source-Community-Software. Dieses inoffizielle Tool ist nicht mit Gaijin Entertainment verbunden, von Gaijin bestätigt oder gesponsert."),

        ["fr"] = InfoPack(new(),
            "Fonctionne automatiquement avec SteamVR, OpenXR et VDXR.", "Thème visuel",
            "Votre centre de contrôle pour War Thunder VR : basculez entre les configurations Écran/VR, gérez les profils d'appareils, configurez Neck Assistant et KeyBind Assistant, et utilisez VTrim pour le trim HOTAS et les courbes d'axes.",
            "NOTES DE VERSION  ·  v1.0 → v{0}",
            "v1.0  ·  ASSISTANT VR DE BASE\n• Bascule des réglages graphiques Écran/VR et intégration du lanceur War Thunder.\n• Neck Assistant pour améliorer la visibilité arrière en VR.\n• Réglages graphiques/commandes enregistrés et gestion de profils de base.\n\nv1.1  ·  KEYBINDS & CONFORT\n• KeyBind Assistant pour les commandes cachées de War Thunder via clavier, souris ou HOTAS.\n• Prise en charge des interrupteurs physiques ON/OFF, états persistants et mises à jour plus sûres.\n• Meilleure gestion d'OpenXR/SteamVR/VDXR et mise à l'échelle de l'interface.\n\nv2.0  ·  VTRIM & PROFILS D'APPAREILS\n• VTrim intégré avec sortie vJoy, routage des axes physiques et trim virtuel.\n• Détection automatique de l'appareil avec profils personnalisés par appareil.\n• Affectations de trim, courbes de réponse, copier/coller et images d'appareils mises en cache.\n\nv2.0.1  ·  VERSION ACTUELLE\n• Profils d'appareils retravaillés avec édition pleine page et points personnalisés sur les courbes.\n• Navigation VTrim, configuration des périphériques/sorties, rendu et mise en page adaptative améliorés.\n• Traductions étendues et corrections du lanceur, des mises à jour, de l'état des profils et du scintillement.\n• Flight Assistant reste désactivé en attente d'une clarification officielle de Gaijin.",
            "Logiciel communautaire gratuit et open source. Cet outil non officiel n'est ni affilié, ni approuvé, ni sponsorisé par Gaijin Entertainment."),

        ["pt"] = InfoPack(new(),
            "Funciona automaticamente com SteamVR, OpenXR e VDXR.", "Tema visual",
            "Seu centro de controle para War Thunder VR: alterne configurações Monitor/VR, gerencie perfis de aeronaves, configure Neck Assistant e KeyBind Assistant e use o VTrim para trim do HOTAS e curvas de eixos.",
            "NOTAS DA VERSÃO  ·  v1.0 → v{0}",
            "v1.0  ·  ASSISTENTE VR PRINCIPAL\n• Alternância de gráficos Monitor/VR e integração com o launcher do War Thunder.\n• Neck Assistant para melhor visibilidade traseira em VR.\n• Configurações de gráficos/controles salvas e gerenciamento básico de perfis.\n\nv1.1  ·  KEYBINDS E QUALIDADE DE VIDA\n• KeyBind Assistant para comandos ocultos do War Thunder usando teclado, mouse ou HOTAS.\n• Suporte a chaves físicas ON/OFF, estados persistentes e atualizações mais seguras.\n• Melhor suporte a OpenXR/SteamVR/VDXR e escala da interface.\n\nv2.0  ·  VTRIM E PERFIS DE AERONAVES\n• VTrim integrado com saída vJoy, roteamento de eixos físicos e trim virtual.\n• Detecção automática de aeronaves com perfis personalizados por aeronave.\n• Bindings de trim, curvas de resposta, copiar/colar e imagens de aeronaves em cache.\n\nv2.0.1  ·  VERSÃO ATUAL\n• Perfis de aeronaves refeitos com edição em página inteira e pontos personalizados nas curvas.\n• Melhor navegação do VTrim, configuração de dispositivos/saída, renderização e layouts responsivos.\n• Mais traduções e correções no launcher, atualizações, estado de perfis e flicker.\n• Flight Assistant permanece desativado enquanto aguardamos esclarecimento oficial da Gaijin.",
            "Software comunitário gratuito e de código aberto. Esta ferramenta não oficial não é afiliada, endossada ou patrocinada pela Gaijin Entertainment."),

        ["pl"] = InfoPack(new(),
            "Działa automatycznie ze SteamVR, OpenXR i VDXR.", "Motyw wizualny",
            "Centrum sterowania War Thunder VR: przełączaj ustawienia Monitor/VR, zarządzaj profilami samolotów, konfiguruj Neck Assistant i KeyBind Assistant oraz używaj VTrim do trymowania HOTAS i krzywych osi.",
            "INFORMACJE O WERSJACH  ·  v1.0 → v{0}",
            "v1.0  ·  PODSTAWOWY ASYSTENT VR\n• Przełączanie grafiki Monitor/VR i integracja z launcherem War Thunder.\n• Neck Assistant poprawiający widoczność do tyłu w VR.\n• Zapisane ustawienia grafiki/sterowania i podstawowe zarządzanie profilami.\n\nv1.1  ·  KEYBINDY I ULEPSZENIA\n• KeyBind Assistant dla ukrytych komend War Thunder z klawiatury, myszy lub HOTAS.\n• Obsługa fizycznych przełączników ON/OFF, zapamiętywanie stanów i bezpieczniejsze aktualizacje.\n• Lepsza obsługa OpenXR/SteamVR/VDXR i skalowanie interfejsu.\n\nv2.0  ·  VTRIM I PROFILE SAMOLOTÓW\n• Wbudowany VTrim z wyjściem vJoy, routingiem osi fizycznych i wirtualnym trymem.\n• Automatyczne wykrywanie samolotu z osobnymi profilami Custom Profile.\n• Bindy trymu, krzywe reakcji, kopiowanie/wklejanie i buforowane obrazy samolotów.\n\nv2.0.1  ·  BIEŻĄCA WERSJA\n• Przebudowane profile samolotów z edycją na całej stronie i własnymi punktami krzywych.\n• Lepsza nawigacja VTrim, konfiguracja urządzeń/wyjścia, renderowanie i responsywny układ.\n• Rozszerzone tłumaczenia oraz poprawki launchera, aktualizacji, stanu profili i migotania.\n• Flight Assistant pozostaje wyłączony do czasu oficjalnego wyjaśnienia ze strony Gaijin.",
            "Darmowe oprogramowanie społecznościowe o otwartym kodzie. To nieoficjalne narzędzie nie jest powiązane, zatwierdzone ani sponsorowane przez Gaijin Entertainment."),

        ["ru"] = InfoPack(new(),
            "Автоматически работает со SteamVR, OpenXR и VDXR.", "Визуальная тема",
            "Центр управления War Thunder VR: переключение настроек Monitor/VR, управление профилями самолётов, настройка Neck Assistant и KeyBind Assistant, а также VTrim для триммирования HOTAS и кривых осей.",
            "ИСТОРИЯ ИЗМЕНЕНИЙ  ·  v1.0 → v{0}",
            "v1.0  ·  БАЗОВЫЙ VR ASSISTANT\n• Переключение графики Monitor/VR и интеграция с лаунчером War Thunder.\n• Neck Assistant для улучшенного обзора назад в VR.\n• Сохранение графики/управления и базовое управление профилями.\n\nv1.1  ·  KEYBINDS И УДОБСТВО\n• KeyBind Assistant для скрытых команд War Thunder с клавиатуры, мыши или HOTAS.\n• Поддержка физических переключателей ON/OFF, сохранение состояний и более безопасные обновления.\n• Улучшенная работа с OpenXR/SteamVR/VDXR и масштабирование интерфейса.\n\nv2.0  ·  VTRIM И ПРОФИЛИ САМОЛЁТОВ\n• Встроенный VTrim с выходом vJoy, маршрутизацией физических осей и виртуальным тримом.\n• Автоматическое определение самолёта и отдельные Custom Profiles.\n• Настройки трима, кривые отклика, копирование/вставка и кэшированные изображения самолётов.\n\nv2.0.1  ·  ТЕКУЩАЯ ВЕРСИЯ\n• Переработанные профили самолётов с полноэкранным редактором и пользовательскими точками кривых.\n• Улучшены навигация VTrim, настройка устройств/выхода, рендеринг и адаптивная разметка.\n• Расширены переводы и исправлены лаунчер, обновления, состояние профилей и мерцание.\n• Flight Assistant остаётся отключённым до официального разъяснения от Gaijin.",
            "Бесплатное программное обеспечение сообщества с открытым исходным кодом. Этот неофициальный инструмент не связан, не одобрен и не спонсируется Gaijin Entertainment."),

        ["uk"] = InfoPack(UkrainianTranslations(),
            "Автоматично працює зі SteamVR, OpenXR і VDXR.", "Візуальна тема",
            "Центр керування War Thunder VR: перемикайте налаштування Monitor/VR, керуйте профілями літаків, налаштовуйте Neck Assistant і KeyBind Assistant та використовуйте VTrim для тримування HOTAS і кривих осей.",
            "НОТАТКИ ДО ВЕРСІЙ  ·  v1.0 → v{0}",
            "v1.0  ·  БАЗОВИЙ VR ASSISTANT\n• Перемикання графіки Monitor/VR та інтеграція з лаунчером War Thunder.\n• Neck Assistant для кращого огляду назад у VR.\n• Збережені налаштування графіки/керування та базове керування профілями.\n\nv1.1  ·  KEYBINDS І ЗРУЧНІСТЬ\n• KeyBind Assistant для прихованих команд War Thunder через клавіатуру, мишу або HOTAS.\n• Підтримка фізичних перемикачів ON/OFF, збереження станів і безпечніші оновлення.\n• Покращена робота OpenXR/SteamVR/VDXR та масштабування інтерфейсу.\n\nv2.0  ·  VTRIM І ПРОФІЛІ ЛІТАКІВ\n• Вбудований VTrim із виходом vJoy, маршрутизацією фізичних осей і віртуальним тримом.\n• Автоматичне визначення літака та окремі Custom Profiles.\n• Прив'язки триму, криві відгуку, копіювання/вставлення та кешовані зображення літаків.\n\nv2.0.1  ·  ПОТОЧНА ВЕРСІЯ\n• Перероблені профілі літаків із повносторінковим редактором і власними точками кривих.\n• Покращено навігацію VTrim, Devices & Output, рендеринг і адаптивне компонування.\n• Розширено переклади та виправлено launcher, update, profile-state і flicker проблеми.\n• Flight Assistant залишається вимкненим до офіційного роз'яснення від Gaijin.",
            "Безкоштовне програмне забезпечення спільноти з відкритим кодом. Цей неофіційний інструмент не пов'язаний, не схвалений і не спонсорується Gaijin Entertainment."),

        ["tr"] = InfoPack(new(),
            "SteamVR, OpenXR ve VDXR ile otomatik olarak çalışır.", "Görsel tema",
            "War Thunder VR kontrol merkeziniz: Monitor/VR ayarları arasında geçiş yapın, uçak profillerini yönetin, Neck Assistant ve KeyBind Assistant'ı yapılandırın ve HOTAS trim ile eksen eğrileri için VTrim'i kullanın.",
            "SÜRÜM NOTLARI  ·  v1.0 → v{0}",
            "v1.0  ·  TEMEL VR ASSISTANT\n• Monitor/VR grafik geçişi ve War Thunder launcher entegrasyonu.\n• VR'da daha iyi arka görüş için Neck Assistant.\n• Kaydedilmiş grafik/kontrol ayarları ve temel profil yönetimi.\n\nv1.1  ·  KEYBINDS VE KULLANIM KOLAYLIĞI\n• Klavye, fare veya HOTAS ile gizli War Thunder komutları için KeyBind Assistant.\n• Fiziksel ON/OFF switch desteği, kalıcı assistant durumları ve daha güvenli güncellemeler.\n• Geliştirilmiş OpenXR/SteamVR/VDXR desteği ve arayüz ölçekleme.\n\nv2.0  ·  VTRIM VE UÇAK PROFİLLERİ\n• vJoy çıkışı, fiziksel eksen yönlendirme ve sanal trim ile entegre VTrim.\n• Uçağa özel Custom Profile ile otomatik uçak algılama.\n• Uçak başına trim bindingleri, tepki eğrileri, kopyala/yapıştır araçları ve önbellek uçak görselleri.\n\nv2.0.1  ·  GÜNCEL SÜRÜM\n• Tam sayfa düzenleme ve özel eğri noktalarıyla yenilenmiş uçak profilleri.\n• Geliştirilmiş VTrim navigasyonu, cihaz/çıkış kurulumu, render ve responsive layout.\n• Genişletilmiş çeviriler ve launcher, güncelleme, profil durumu ve flicker düzeltmeleri.\n• Flight Assistant, Gaijin'den resmi açıklama beklenirken devre dışı kalır.",
            "Ücretsiz ve açık kaynaklı topluluk yazılımı. Bu resmi olmayan araç Gaijin Entertainment ile bağlantılı değildir, Gaijin tarafından onaylanmamış veya sponsor edilmemiştir."),

        ["el"] = InfoPack(new(),
            "Λειτουργεί αυτόματα με SteamVR, OpenXR και VDXR.", "Οπτικό θέμα",
            "Το κέντρο ελέγχου για War Thunder VR: εναλλαγή Monitor/VR ρυθμίσεων, διαχείριση προφίλ αεροσκαφών, ρύθμιση Neck Assistant και KeyBind Assistant και χρήση VTrim για HOTAS trim και καμπύλες αξόνων.",
            "ΣΗΜΕΙΩΣΕΙΣ ΕΚΔΟΣΕΩΝ  ·  v1.0 → v{0}",
            "v1.0  ·  ΒΑΣΙΚΟΣ VR ASSISTANT\n• Εναλλαγή γραφικών Monitor/VR και ενσωμάτωση launcher του War Thunder.\n• Neck Assistant για καλύτερη οπίσθια ορατότητα σε VR.\n• Αποθηκευμένες ρυθμίσεις γραφικών/χειρισμών και βασική διαχείριση προφίλ.\n\nv1.1  ·  KEYBINDS & ΒΕΛΤΙΩΣΕΙΣ\n• KeyBind Assistant για κρυφές εντολές War Thunder με πληκτρολόγιο, ποντίκι ή HOTAS.\n• Υποστήριξη φυσικών διακοπτών ON/OFF, μόνιμες καταστάσεις και ασφαλέστερα updates.\n• Βελτιωμένη υποστήριξη OpenXR/SteamVR/VDXR και κλιμάκωση UI.\n\nv2.0  ·  VTRIM & ΠΡΟΦΙΛ ΑΕΡΟΣΚΑΦΩΝ\n• Ενσωματωμένο VTrim με έξοδο vJoy, routing φυσικών αξόνων και virtual trim.\n• Αυτόματη ανίχνευση αεροσκάφους με ξεχωριστά Custom Profiles.\n• Trim bindings, response curves, copy/paste και cached εικόνες ανά αεροσκάφος.\n\nv2.0.1  ·  ΤΡΕΧΟΥΣΑ ΕΚΔΟΣΗ\n• Νέα επεξεργασία προφίλ αεροσκαφών σε πλήρη σελίδα με custom σημεία καμπύλης.\n• Βελτιωμένη πλοήγηση VTrim, ρύθμιση συσκευών/εξόδου, rendering και responsive layouts.\n• Περισσότερες μεταφράσεις και διορθώσεις σε launcher, updates, profile state και flicker.\n• Το Flight Assistant παραμένει απενεργοποιημένο μέχρι επίσημη διευκρίνιση από τη Gaijin.",
            "Δωρεάν λογισμικό κοινότητας ανοικτού κώδικα. Αυτό το ανεπίσημο εργαλείο δεν συνδέεται, δεν εγκρίνεται και δεν χρηματοδοτείται από τη Gaijin Entertainment."),

        ["ro"] = InfoPack(new(),
            "Funcționează automat cu SteamVR, OpenXR și VDXR.", "Temă vizuală",
            "Centrul tău de control pentru War Thunder VR: comută setările Monitor/VR, gestionează profilurile aeronavelor, configurează Neck Assistant și KeyBind Assistant și folosește VTrim pentru trim HOTAS și curbe de axe.",
            "NOTE DE VERSIUNE  ·  v1.0 → v{0}",
            "v1.0  ·  ASISTENT VR DE BAZĂ\n• Comutare grafică Monitor/VR și integrare cu launcherul War Thunder.\n• Neck Assistant pentru vizibilitate mai bună în spate în VR.\n• Setări grafice/controale salvate și administrare de bază a profilurilor.\n\nv1.1  ·  KEYBINDS ȘI ÎMBUNĂTĂȚIRI\n• KeyBind Assistant pentru comenzi War Thunder ascunse folosind tastatură, mouse sau HOTAS.\n• Suport pentru switch-uri fizice ON/OFF, stări persistente și actualizări mai sigure.\n• Suport îmbunătățit OpenXR/SteamVR/VDXR și scalare UI.\n\nv2.0  ·  VTRIM ȘI PROFILURI DE AERONAVE\n• VTrim integrat cu ieșire vJoy, rutare axe fizice și trim virtual.\n• Detectare automată a aeronavei cu profiluri Custom Profile dedicate.\n• Bindinguri trim, curbe de răspuns, copy/paste și imagini de aeronave în cache.\n\nv2.0.1  ·  VERSIUNEA CURENTĂ\n• Profiluri de aeronave refăcute cu editare pe pagină completă și puncte custom pe curbe.\n• Navigare VTrim, configurare dispozitive/ieșire, randare și layout responsiv îmbunătățite.\n• Localizare extinsă și remedieri pentru launcher, update, profile state și flicker.\n• Flight Assistant rămâne dezactivat până la o clarificare oficială din partea Gaijin.",
            "Software comunitar gratuit și open-source. Acest instrument neoficial nu este afiliat, aprobat sau sponsorizat de Gaijin Entertainment."),

        ["he"] = InfoPack(HebrewTranslations(),
            "פועל אוטומטית עם SteamVR, OpenXR ו-VDXR.", "ערכת נושא חזותית",
            "מרכז השליטה שלך ל-War Thunder VR: מעבר בין הגדרות Monitor/VR, ניהול פרופילי מטוסים, הגדרת Neck Assistant ו-KeyBind Assistant ושימוש ב-VTrim לקיזוז HOTAS ולעקומות צירים.",
            "הערות גרסה  ·  v1.0 → v{0}",
            "v1.0  ·  עוזר VR בסיסי\n• מעבר בין גרפיקת Monitor/VR ושילוב עם משגר War Thunder.\n• Neck Assistant לשיפור הראייה לאחור ב-VR.\n• שמירת הגדרות גרפיקה/בקרה וניהול פרופילים בסיסי.\n\nv1.1  ·  KEYBINDS ושיפורי שימוש\n• KeyBind Assistant לפקודות War Thunder נסתרות דרך מקלדת, עכבר או HOTAS.\n• תמיכה במתגים פיזיים ON/OFF, שמירת מצבים ועדכונים בטוחים יותר.\n• תמיכה משופרת ב-OpenXR/SteamVR/VDXR וקנה מידה לממשק.\n\nv2.0  ·  VTRIM ופרופילי מטוסים\n• VTrim משולב עם פלט vJoy, ניתוב צירים פיזיים ו-trim וירטואלי.\n• זיהוי אוטומטי של המטוס עם Custom Profiles ייעודיים.\n• הקצאות trim, עקומות תגובה, העתקה/הדבקה ותמונות מטוסים במטמון.\n\nv2.0.1  ·  הגרסה הנוכחית\n• פרופילי מטוסים שעוצבו מחדש עם עריכה בעמוד מלא ונקודות עקומה מותאמות.\n• ניווט VTrim, הגדרת התקנים/פלט, רינדור ופריסה רספונסיבית משופרים.\n• תרגומים מורחבים ותיקונים ל-launcher, לעדכונים, למצב הפרופילים ול-flicker.\n• Flight Assistant נשאר כבוי עד לקבלת הבהרה רשמית מ-Gaijin.",
            "תוכנת קהילה חינמית ובקוד פתוח. כלי לא רשמי זה אינו קשור ל-Gaijin Entertainment, אינו מאושר ואינו ממומן על ידה."),

        ["zh-Hans"] = InfoPack(new(),
            "自动支持 SteamVR、OpenXR 和 VDXR。", "视觉主题",
            "War Thunder VR 控制中心：切换显示器/VR 设置，管理飞机配置，设置 Neck Assistant 和 KeyBind Assistant，并使用 VTrim 进行 HOTAS 配平和轴曲线调整。",
            "版本说明  ·  v1.0 → v{0}",
            "v1.0  ·  核心 VR 助手\n• 显示器/VR 图形设置切换，并集成 War Thunder 启动器。\n• Neck Assistant 提升 VR 中的后方观察能力。\n• 保存图形/控制设置并提供基础配置管理。\n\nv1.1  ·  按键绑定与易用性\n• KeyBind Assistant 可通过键盘、鼠标或 HOTAS 触发 War Thunder 隐藏命令。\n• 支持实体 ON/OFF 开关、保存助手状态以及更安全的应用更新。\n• 改进 OpenXR/SteamVR/VDXR 支持和界面缩放。\n\nv2.0  ·  VTRIM 与飞机配置\n• 集成 VTrim，支持 vJoy 输出、物理轴路由和虚拟配平。\n• 自动识别飞机并使用对应的 Custom Profile。\n• 每架飞机可保存配平绑定、响应曲线、复制/粘贴工具和缓存飞机图片。\n\nv2.0.1  ·  当前版本\n• 重做飞机配置编辑器，支持整页编辑和自定义曲线控制点。\n• 改进 VTrim 导航、设备/输出设置、渲染和响应式布局。\n• 扩展多语言支持，并修复启动器、更新、配置状态和闪烁问题。\n• Flight Assistant 暂时保持禁用，等待 Gaijin 的正式说明。",
            "免费开源的社区软件。本非官方工具与 Gaijin Entertainment 无隶属、认可或赞助关系。")
    };

    private static Dictionary<string, string> InfoPack(
        Dictionary<string, string> text,
        string runtimeSupport,
        string theme,
        string purpose,
        string changesTitle,
        string changesText,
        string legal)
    {
        text["Neck.RuntimeSupport"] = runtimeSupport;
        text["Options.Theme"] = theme;
        text["Tray.AppName"] = "War Thunder VR Assistant";
        text["Info.Title"] = "WAR THUNDER VR ASSISTANT";
        text["Info.Purpose"] = purpose;
        text["Info.ChangesTitle"] = changesTitle.Replace("v1.0 → v{0}", "{0}");
        if (changesTitle.StartsWith("PATCH NOTES", StringComparison.Ordinal))
            text["Info.ChangesTitle"] = "Version {0} Patch Notes";
        // Keep translated features, discard historical per-version headings.
        text["Info.ChangesText"] = changesTitle.StartsWith("PATCH NOTES", StringComparison.Ordinal)
            ? ReleaseNotes.Features
            : string.Join("\n\n", changesText.Split('\n').Where(line => line.TrimStart().StartsWith("•", StringComparison.Ordinal)));
        text["Info.OpenSource"] = legal;
        return text;
    }
}
