using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>The name families FS27's fictional identities are drawn from. A nationality code picks the family, so a player keeps a name that fits where he is from.</summary>
    public enum NameCulture
    {
        Anglo = 0,
        Spanish,
        LatinAmerican,
        Portuguese,
        French,
        WestAfricanFrench,
        WestAfricanEnglish,
        Italian,
        German,
        Dutch,
        Nordic,
        EastEuropean,
        Arabic,
        Turkish,
        Japanese,
        Korean
    }

    /// <summary>Given names and family names of one culture. Invented combinations of ordinary names; none is taken from a footballer.</summary>
    public sealed class NamePool
    {
        public readonly string[] Given;
        public readonly string[] Family;
        public NamePool(string given, string family) { Given = given.Split(';'); Family = family.Split(';'); }
    }

    /// <summary>The data behind <see cref="IdentityGenerator"/>: the pools and which nationality code uses which. Editing a pool changes future generations only; the identities already in the data file never move.</summary>
    public static class NamePools
    {
        private static readonly Dictionary<NameCulture, NamePool> Pools = Build();
        private static readonly Dictionary<string, NameCulture> CultureOf = BuildCodes();

        public static NamePool Of(NameCulture culture) { return Pools[culture]; }

        /// <summary>The culture of a nationality code. A code with no entry uses <see cref="NameCulture.Anglo"/> (the generator reports it).</summary>
        public static NameCulture CultureForCode(string code, out bool known)
        {
            known = code != null && CultureOf.ContainsKey(code);
            return known ? CultureOf[code] : NameCulture.Anglo;
        }

        public static IEnumerable<string> KnownCodes { get { return CultureOf.Keys; } }

        private static Dictionary<string, NameCulture> BuildCodes()
        {
            var d = new Dictionary<string, NameCulture>();
            void Add(NameCulture c, string codes) { foreach (string code in codes.Split(',')) d[code] = c; }
            Add(NameCulture.Anglo, "ENG,SCO,WAL,NIR,IRL,AUS,NZL,CAN,USA,RSA");
            Add(NameCulture.Spanish, "ESP");
            Add(NameCulture.LatinAmerican, "ARG,URU,VEN,ECU,COL,MEX,PER,CHI,PAR,BOL,CRC");
            Add(NameCulture.Portuguese, "POR,BRA,CPV");
            Add(NameCulture.French, "FRA,BEL,LUX");
            Add(NameCulture.WestAfricanFrench, "SEN,CMR,GAB,CIV,MLI,GUI,COD,ALG");
            Add(NameCulture.WestAfricanEnglish, "NGA,GHA,SLE");
            Add(NameCulture.Italian, "ITA");
            Add(NameCulture.German, "GER,SUI,AUT");
            Add(NameCulture.Dutch, "NED");
            Add(NameCulture.Nordic, "NOR,SWE,DEN,FIN,ISL");
            Add(NameCulture.EastEuropean, "CRO,SVN,SVK,UKR,RUS,HUN,GEO,KVX,ROU,POL,CZE,SRB,BIH,ALB,GRE,BUL");
            Add(NameCulture.Arabic, "MAR,EGY,TUN");
            Add(NameCulture.Turkish, "TUR");
            Add(NameCulture.Japanese, "JPN");
            Add(NameCulture.Korean, "KOR");
            return d;
        }

        private static Dictionary<NameCulture, NamePool> Build()
        {
            var d = new Dictionary<NameCulture, NamePool>();
            d[NameCulture.Anglo] = new NamePool(
                "Callum;Declan;Harvey;Jasper;Rory;Tobias;Wesley;Linus;Nathan;Oakley;Percy;Reuben;Seth;Toby;Ewan;Finlay;Gareth;Hamish;Isaac;Jonty;Kieran;Lachlan;Malcolm;Nigel;Orrin;Perry;Rhys;Stuart;Tristan;Vaughn;Warren;Xander;Alistair;Barnaby;Cormac;Dermot;Elliot;Fergus;Graeme;Heath;Idris;Jarvis;Kit;Lowell;Monty;Nolan",
                "Ashworth;Bramley;Calloway;Dunmore;Eastwood;Fairbanks;Garrick;Hartwell;Ingram;Jessop;Kirkwood;Lockhart;Marlowe;Northcott;Oakes;Pemberton;Quill;Redfern;Sutcliffe;Thornley;Underhill;Vance;Wetherby;Yardley;Aldous;Blackwood;Cresswell;Denholm;Ellerby;Fenwick;Gresham;Holloway;Irvine;Jarrow;Kendrick;Langley;Merriweather;Nethercott;Ormerod;Prescott;Radcliffe;Strickland;Tennant;Upshaw;Whitcombe;Ashcroft;Binnie;Coxwell;Duffield;Egerton;Faulkner;Gilchrist;Hargreave;Inglis;Kelsall;Lowther;Maddox;Nuttall");
            d[NameCulture.Spanish] = new NamePool(
                "Adrián;Bernabé;Cristóbal;Dídac;Eloy;Fabián;Gael;Hilario;Iker;Joaquín;Leandro;Marcelo;Néstor;Octavio;Pelayo;Quico;Rodrigo;Samuel;Tiago;Ulises;Valentín;Xabier;Aitor;Borja;Casimiro;Damián;Efrén;Fermín;Gonzalo;Heliodoro;Ignacio;Jairo;Lisandro;Mauro;Nicanor;Olegario;Pascual;Ramiro;Severo;Teodoro",
                "Almazán;Bermejo;Cañizares;Dueñas;Escobedo;Fontanals;Garrido;Hontoria;Iglesias;Jaramillo;Landeira;Maldonado;Narváez;Olmedo;Palencia;Quintanilla;Rebollo;Saavedra;Toledano;Urdangarín;Valdivia;Yáñez;Zubiaur;Abellán;Beltrán;Carrasco;Dorado;Echeverría;Ferreiro;Galindo;Heredia;Ibarra;Juncal;Lasheras;Montalvo;Navarrete;Ordóñez;Pellicer;Roldán;Salcedo;Tejedor;Uriarte;Varela;Zamudio;Arrieta;Bustamante;Cordero;Delgado;Espinosa;Frías");
            d[NameCulture.LatinAmerican] = new NamePool(
                "Agustín;Baltasar;Camilo;Dante;Emiliano;Facundo;Genaro;Horacio;Ismael;Julián;Kevin;Lautaro;Maximiliano;Nahuel;Orlando;Patricio;Renzo;Santiago;Thiago;Uriel;Valentino;Wilmer;Yago;Zacarías;Abel;Bruno;César;Diego;Elías;Franco;Gastón;Hernán;Iván;Jerónimo;Lucio;Mateo;Nicolás;Osvaldo;Pablo;Ramón",
                "Altamirano;Barrientos;Cabrera;Duarte;Escalante;Figueroa;Gallardo;Herrera;Insúa;Jiménez;Lagos;Medina;Nogueira;Oyarzún;Pizarro;Quiroga;Rivadeneira;Sandoval;Tapia;Urquiza;Vallejos;Yrigoyen;Zalazar;Aguilera;Bordón;Campos;Dávila;Echegaray;Funes;Guzmán;Haro;Ibáñez;Jofré;Larrea;Montiel;Naranjo;Olivera;Peralta;Ramos;Soria;Troncoso;Uribe;Vergara;Zárate;Acevedo;Bustos;Cardozo;Domínguez;Estrada;Fierro");
            d[NameCulture.Portuguese] = new NamePool(
                "Aurélio;Baltazar;Caetano;Duarte;Eurico;Fausto;Gaspar;Helder;Isidro;Joel;Leonel;Matias;Nuno;Otávio;Plácido;Quirino;Rúben;Salvador;Telmo;Ulisses;Valter;Zeca;Ananias;Benício;Cláudio;Dinis;Edmundo;Fábio;Gilberto;Hugo;Ivo;Jorge;Lourenço;Mário;Nélson;Osvaldo;Paulino;Rolando;Sérgio;Tomé",
                "Albuquerque;Barroso;Cardoso;Damasceno;Esteves;Ferrão;Gouveia;Henriques;Jordão;Lacerda;Magalhães;Nogueira;Oliveira;Pestana;Queiroz;Rebelo;Salgado;Teles;Uchôa;Vasconcelos;Xavier;Amaral;Brandão;Camarinha;Dantas;Eusébio;Faria;Guedes;Honório;Ilharco;Lobato;Marinho;Neves;Ornelas;Pimentel;Quintela;Rangel;Seabra;Tavares;Valadares;Zagalo;Aragão;Bettencourt;Cabral;Domingues;Evangelista;Furtado;Gama;Hilário;Isidoro");
            d[NameCulture.French] = new NamePool(
                "Achille;Baptiste;Clément;Dorian;Étienne;Florian;Gaspard;Hector;Idriss;Jules;Killian;Lucien;Maxence;Noé;Olivier;Pascal;Quentin;Rémi;Sacha;Théo;Ulysse;Valère;Yann;Zacharie;Aurélien;Bastien;Corentin;Damien;Émeric;Fabrice;Gautier;Hugues;Ilan;Jérôme;Loïc;Mathis;Nolan;Oscar;Paulin;Romain",
                "Armand;Bellamy;Chevalier;Delacroix;Estève;Fontaine;Gaillard;Hamelin;Jouvet;Lacombe;Marchand;Neveu;Ollivier;Perrault;Quesnel;Rochefort;Sauvage;Tessier;Valmont;Aubry;Boulanger;Castex;Deschamps;Ferrand;Granger;Hébert;Imbert;Lemaire;Mercier;Noël;Pelletier;Renaud;Sabatier;Thibault;Vasseur;Auclair;Besnard;Caron;Dupuis;Escoffier;Fabre;Gerbier;Huet;Lavigne;Monnet;Navarre;Odet;Pasquier;Rivière;Salmon");
            d[NameCulture.WestAfricanFrench] = new NamePool(
                "Abdou;Bakary;Cheikh;Demba;Ebrima;Famara;Gora;Habib;Ismaïla;Kalidou;Lamine;Mamadou;Ndiaga;Omar;Pape;Sekou;Tidiane;Ousmane;Yacouba;Zakaria;Adama;Boubacar;Cédric;Daouda;Emmanuel;Fodé;Guy;Hamidou;Ibrahima;Jean-Luc;Kader;Landry;Moussa;Nestor;Oumar;Prosper",
                "Ba;Cissé;Diakhaté;Faye;Gueye;Kouyaté;Ndoye;Sarr;Thiam;Wade;Camara;Diatta;Fofana;Keita;Mbaye;Ndiaye;Sow;Touré;Bangoura;Coulibaly;Diouf;Kamara;Mbengue;Niang;Samb;Traoré;Bamba;Konaté;Sylla;Tchakounté;Zongo;Abena;Biyik;Ebongue;Fokou;Kamdem;Nkoulou;Tagne;Ateba;Bikoï");
            d[NameCulture.WestAfricanEnglish] = new NamePool(
                "Abiola;Babatunde;Chidi;Dayo;Emeka;Femi;Gbenga;Ikenna;Jide;Kelechi;Lekan;Obinna;Segun;Tunde;Uche;Yaw;Kofi;Kwame;Kwesi;Nana;Papa;Seth;Tetteh;Akwasi;Bright;Chukwu;Dele;Ebuka;Folarin;Ifeanyi",
                "Adeyemi;Balogun;Chukwuma;Dosunmu;Eze;Fashola;Gbadamosi;Ibekwe;Jegede;Kalu;Lawal;Nwosu;Okafor;Onyeama;Salami;Tella;Udoh;Yakubu;Acheampong;Boateng;Darko;Frimpong;Gyamfi;Mensah;Nkrumah;Osei;Quaye;Sarpong;Tagoe;Yeboah;Addo;Badu;Danquah;Kyei;Opoku");
            d[NameCulture.Italian] = new NamePool(
                "Amedeo;Bernardo;Cesare;Dario;Ennio;Fabrizio;Gianluca;Ippolito;Lorenzo;Massimo;Nunzio;Orazio;Pellegrino;Raffaele;Silvio;Tancredi;Umberto;Valerio;Vittorio;Zeno;Alberto;Bruno;Corrado;Domenico;Elio;Federico;Gino;Livio;Matteo;Nicola;Osvaldo;Piero;Renato;Saverio;Tullio;Ugo",
                "Albertini;Bellandi;Caracciolo;Dellavalle;Esposito;Faccini;Gentile;Iacovelli;Lombardi;Mazzanti;Nardone;Orlandi;Paganelli;Quaranta;Ruggeri;Sabatini;Tedesco;Ventura;Zanotti;Ardizzone;Barbieri;Cerutti;Dalmasso;Fantoni;Giordani;Landolfi;Marchetti;Nicolosi;Pellegrini;Ricciardi;Santoro;Tagliaferri;Vitale;Bonfiglio;Cattaneo;Del Moro;Fiorentini;Gallo;Lunardi;Moretti;Palumbo;Rinaldi;Spadaro;Tozzi");
            d[NameCulture.German] = new NamePool(
                "Albrecht;Bastian;Dietmar;Eckart;Florian;Gerrit;Hannes;Ingo;Jonas;Konrad;Lennart;Matthias;Norbert;Oskar;Philipp;Rüdiger;Sven;Torsten;Ulrich;Volker;Wolfram;Ansgar;Berthold;Claus;Dirk;Emil;Falk;Gunnar;Henrik;Jens;Kilian;Lars;Merten;Niklas;Otto;Rainer",
                "Ackermann;Brandt;Dorfmeister;Eberhard;Feldmann;Gerlach;Hartmann;Immel;Jungblut;Kellermann;Lindner;Maurer;Neumeier;Oberholzer;Pfeiffer;Rosenthal;Schwarzer;Tiedemann;Ullmann;Vogelsang;Wendler;Zimmerer;Aschenbach;Bergmann;Claussen;Detmers;Ebner;Freudenberg;Grabowski;Hellwig;Jansen;Kiefer;Lutz;Mahler;Nowack;Ostermann;Reinhardt;Sattler;Thalmann;Voss;Winkler;Ziegler;Amsler;Baumgartner;Dubach;Eugster;Gisler;Hunziker;Zbinden");
            d[NameCulture.Dutch] = new NamePool(
                "Aart;Bram;Cas;Daan;Eelco;Floris;Gijs;Hidde;Ivo;Jelle;Koen;Luuk;Maarten;Niels;Olaf;Pim;Quinten;Rutger;Sander;Teun;Wouter;Yorick;Arjen;Bas;Dirk;Evert;Freek;Gerben;Hein;Joost;Kees;Lodewijk;Merijn;Ruud",
                "Aalbers;Bosman;Dekkers;Eijkelboom;Fransen;Gerritsen;Hoekstra;Ijsselstein;Jacobs;Kuipers;Lammers;Mulder;Nijhuis;Oosterhuis;Peeters;Rademaker;Smeets;Termeer;Veldkamp;Wolters;Zijlstra;Brouwers;Claassen;De Wit;Everts;Faber;Gerrits;Hendriks;Jonker;Kok;Leeflang;Maas;Noordhoek;Prins;Scholten;Van Dijk;Van Rijn;Vermeer;Wester;Zwart");
            d[NameCulture.Nordic] = new NamePool(
                "Anders;Bjørn;Casper;Dag;Erlend;Frode;Gunnar;Halvard;Ivar;Jonas;Kristoffer;Leif;Magnus;Njål;Oddvar;Petter;Rasmus;Sindre;Tormod;Ulrik;Viggo;Axel;Emil;Fredrik;Gustav;Henrik;Isak;Johan;Karl;Linus;Mikael;Nils",
                "Aasen;Berglund;Dahlgren;Eklund;Fjeldstad;Gundersen;Haugland;Isaksen;Jørgensen;Kaldestad;Lindgren;Moberg;Nordvik;Oppedal;Persson;Rasmussen;Sandvik;Thorsen;Ulvestad;Vikström;Wallin;Åkerblom;Bergström;Carlsen;Dalen;Engström;Frøland;Granlund;Holmgren;Ingebrigtsen;Kjellberg;Lundqvist;Mikkelsen;Nygård;Ottesen;Strand");
            d[NameCulture.EastEuropean] = new NamePool(
                "Aleksandar;Bogdan;Dragan;Emil;Filip;Goran;Hristo;Ivo;Jakub;Kamil;Lukas;Marek;Nikola;Oleg;Petar;Radek;Stefan;Tomasz;Vasyl;Zoran;Andrei;Boris;Cristian;Damir;Eugen;Gheorghe;Hamza;Ilija;Janos;Kristijan;Levan;Mihail;Nemanja;Ondrej;Pavel;Ruslan",
                "Andrić;Babić;Cerny;Dvořák;Filipović;Gavrilović;Horvat;Ilić;Jovanović;Kovalenko;Lukač;Marković;Novotný;Orlov;Petrenko;Radić;Stanković;Tkachenko;Urban;Vlahović;Zelenka;Balog;Czerwiński;Dobrev;Eftimov;Grigoryan;Hajdu;Ionescu;Janković;Kozłowski;Lazarević;Mazur;Nagy;Oprea;Popescu;Rusu;Szabó;Tomić;Varga;Wojciechowski;Zielinski;Beridze;Gelashvili;Kapanadze;Lomidze");
            d[NameCulture.Arabic] = new NamePool(
                "Abdelkader;Bilal;Chakib;Driss;Elias;Fouad;Ghali;Hamza;Ilyas;Jalal;Karim;Larbi;Mehdi;Nabil;Omar;Rachid;Samir;Tarek;Walid;Yassine;Zakaria;Adnan;Badr;Hicham;Ismail;Khalid;Mounir;Nasser;Othmane;Reda",
                "Alaoui;Benali;Chraibi;Doukkali;El Fassi;Filali;Guerrouj;Hakimi;Idrissi;Jabrane;Kettani;Lahlou;Mansouri;Naciri;Ouazzani;Qadiri;Sebti;Tazi;Wahbi;Zniber;Abdelaziz;Badawi;Darwish;Farouk;Gamal;Hassanein;Ibrahim;Mekawy;Nassar;Shawky;Taher");
            d[NameCulture.Turkish] = new NamePool(
                "Alper;Berkay;Cem;Deniz;Efe;Furkan;Gökhan;Hakan;İlker;Kaan;Levent;Murat;Necati;Onur;Polat;Rüzgar;Selim;Tolga;Umut;Volkan;Yiğit;Zafer;Arda;Barış;Caner;Doruk;Emre;Ferhat;Görkem;Halil",
                "Akbulut;Bozkurt;Çelik;Demirci;Erdem;Gökçe;Işık;Kaplan;Özdemir;Polater;Sarıoğlu;Tekin;Uçar;Yalçın;Aksoy;Başaran;Coşkun;Dinç;Ekinci;Güneş;Karaca;Önal;Sezer;Toprak;Yıldırım;Altıntaş;Çakır;Duman;Eren;Korkmaz");
            d[NameCulture.Japanese] = new NamePool(
                "Akira;Daichi;Eiji;Haruto;Isamu;Jun;Kenta;Makoto;Naoki;Ren;Shota;Takumi;Yuto;Hiroki;Kaito;Riku;Sora;Taiga;Yamato;Zenji",
                "Aizawa;Fujimoto;Hayashida;Ishikura;Kuramoto;Matsuzaki;Nakahara;Okabe;Sakurai;Takeshita;Uemura;Yamashiro;Arakawa;Kitagawa;Mizuno;Nomura;Shirakawa;Tachibana;Wakabayashi;Yoshizawa");
            d[NameCulture.Korean] = new NamePool(
                "Dohyun;Gunwoo;Hyunjin;Jaemin;Joonho;Minjae;Seojun;Taeyang;Woojin;Yuchan;Beomsoo;Changmin;Eunwoo;Hojun;Jiho",
                "Baek;Cho;Gwon;Han;Jeong;Kwon;Moon;Nam;Oh;Ryu;Seo;Yoon;Bae;Hong;Shin");
            return d;
        }
    }
}
