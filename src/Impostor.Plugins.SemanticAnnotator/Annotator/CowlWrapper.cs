using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Xsl;
using cowl;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using System.IO;
using System.Text.RegularExpressions;


namespace Impostor.Plugins.SemanticAnnotator.Utils
{


    public class CowlWrapper : ILibrary
    {
        public static CowlManager? Manager { get; private set; }
        public static CowlOntology? Ontology { get; private set; }


        public static List<nint> instancesToRelease = new List<nint>();

        // --- Definizioni IRI e Utility ---

        /// L'IRI base (namespace) per l'ontologia principale dell'applicazione.
        public const string BaseOntologyIRI = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/";

        /// IRI completo per la classe OWL 'Player'
        public const string PlayerClassIRI = BaseOntologyIRI + "Player"; // ".../AmongUs/Player"

        // Costanti per gli IRI delle proprietà oggetto principali.
        public const string IsInRoomPropertyIRI = BaseOntologyIRI + "isInRoom";
        public const string HasContextualPositionPropertyIRI = BaseOntologyIRI + "hasContextualPosition";
        public const string HasConfidenceLevelPropertyIRI = BaseOntologyIRI + "hasConfidenceLevel";

        /// Sanitizza una stringa per renderla valida come frammento di IRI (la parte dopo '#' o '/').
        public static string SanitizeForIri(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "UnnamedEntity";
            string sanitized = Regex.Replace(input, @"[^a-zA-Z0-9_.-]", "_");
            sanitized = sanitized.Replace(" ", "_").Replace("(", "").Replace(")", "")
                               .Replace(":", "_").Replace("/", "_").Replace(",", ".");
            if (sanitized.Length > 0 && char.IsDigit(sanitized[0])) sanitized = "N" + sanitized;
            sanitized = Regex.Replace(sanitized, @"_+", "_");
            if (sanitized.Length > 1) sanitized = sanitized.Trim('_');
            if (string.IsNullOrWhiteSpace(sanitized) || (input != null && input.Length > 0 && char.IsDigit(input[0]) && sanitized == "N"))
            {
                if (string.IsNullOrWhiteSpace(input) || input.All(c => !char.IsLetterOrDigit(c))) return "SanitizedEmpty";
                return "SanitizedFallback_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            }
            return sanitized;
        }

        // Metodi helper per costruire IRI completi per classi dinamiche.
        public static string GetRoomClassIri(string roomName) => BaseOntologyIRI + SanitizeForIri(roomName);
        public static string GetContextualPositionClassIri(string contextualLabel) => BaseOntologyIRI + SanitizeForIri(contextualLabel);
        public static string GetConfidenceLevelClassIri(string confidenceOwlLabel) => BaseOntologyIRI + SanitizeForIri(confidenceOwlLabel);
        public static string GetPlayerIndividualIri(string playerSuffix) => BaseOntologyIRI + SanitizeForIri(playerSuffix);


        /// Inizializza le istanze statiche `Manager` e `Ontology`.
        /// Configura l'ontologia con l'IRI base specificato e registra il prefisso di default.
        /// Questo metodo è destinato ad essere chiamato una volta dal componente principale
        /// che utilizza l'ontologia condivisa
        public static void InitializeStaticOntology(string ontologyBaseIriToSet)
        {
            try
            {
                // Inizializza la libreria COWL.
                var initResult = cowl_config.CowlInit();
                if (initResult != CowlRet.COWL_OK && initResult.ToString() != "COWL_WARN_ALREADY_INIT")
                    Console.Error.WriteLine($"Attenzione: Inizializzazione Cowl ha restituito {initResult}.");

                // Crea il Manager statico se non esiste.
                if (Manager == null || Manager.__Instance == IntPtr.Zero)
                {
                    Manager = cowl_manager.CowlManager();
                    if (Manager == null || Manager.__Instance == IntPtr.Zero)
                        throw new InvalidOperationException("Impossibile creare CowlWrapper.Manager.");
                }

                // Ottiene/Crea l'Ontologia statica se non esiste.
                if (Ontology == null || Ontology.__Instance == IntPtr.Zero)
                {
                    CowlOntologyId anonId = cowl_ontology_id.CowlOntologyIdAnonymous();
                    if (anonId.__Instance == IntPtr.Zero) // Controllo sull'handle
                        throw new InvalidOperationException("CowlOntologyIdAnonymous() fallito.");
                    Ontology = cowl_manager.CowlManagerGetOntology(Manager, anonId);
                    if (Ontology == null || Ontology.__Instance == IntPtr.Zero)
                        throw new InvalidOperationException("Impossibile ottenere CowlWrapper.Ontology.");
                }

                UString baseIriUstr = default;
                CowlIRI baseCowlIri = default;
                UString prefixEmptyUstr = default; 
                UString prefixBaseIriUstr = default;
                CowlSymTable symTable = default;
                try
                {
                    // Imposta l'IRI dell'ontologia.
                    string actualOntoIri = ontologyBaseIriToSet.TrimEnd('#', '/'); // L'IRI dell'ontologia non dovrebbe avere # o / alla fine.
                    baseIriUstr = UString.UstringCopyBuf(actualOntoIri);
                    baseCowlIri = cowl_iri.CowlIriFromString(baseIriUstr);
                    // Controlla che Ontology e baseCowlIri siano validi prima di usarli.
                    if (baseCowlIri != null && baseCowlIri.__Instance != IntPtr.Zero && Ontology != null)
                        cowl_ontology.CowlOntologySetIri(Ontology, baseCowlIri);
                    else
                        Console.Error.WriteLine($"ATTENZIONE: Impossibile creare CowlIRI o Ontology è null per {actualOntoIri}");

                    // Registra il prefisso di default (vuoto) per l'IRI base.
                    prefixEmptyUstr = UString.UstringCopyBuf("");
                    prefixBaseIriUstr = UString.UstringCopyBuf(ontologyBaseIriToSet); // L'IRI del prefisso può avere # o / finale.
                    if (Ontology != null) // Controlla che Ontology sia valido.
                    {
                        symTable = cowl_ontology.CowlOntologyGetSymTable(Ontology);
                        if (symTable != null && symTable.__Instance != IntPtr.Zero)
                            cowl_sym_table.CowlSymTableRegisterPrefixRaw(symTable, prefixEmptyUstr, prefixBaseIriUstr, true /* sovrascrivi */);
                        else
                            throw new InvalidOperationException("Impossibile ottenere SymTable dall'Ontologia statica.");
                    }
                }
                finally
                { 
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore in InitializeStaticOntology: {ex.Message}\n{ex.StackTrace}");
                throw; // Rilancia per segnalare fallimento critico.
            }
        }

        public static CowlRet CreateIndividual(CowlOntology targetOnto, string individualIri, IEnumerable<CowlClass> classes, IEnumerable<CowlObjQuant> objQuants)
        {
            var operands = cowl_vector.UvecCowlObjectPtr();
            foreach (var cls in classes) cowl_vector.UvecPushCowlObjectPtr(operands, cls.__Instance);
            foreach (var quant in objQuants) cowl_vector.UvecPushCowlObjectPtr(operands, quant.__Instance);

            CowlVector vec = cowl_vector.CowlVector(operands);
            var exp = cowl_nary_bool.CowlNaryBool(CowlNAryType.COWL_NT_INTERSECT, vec); 
            instancesToRelease.Add(exp.__Instance); 

            var ind = cowl_named_ind.CowlNamedIndFromString(UString.UstringCopyBuf(individualIri)); 
            instancesToRelease.Add(ind.__Instance); 

            var axiom = cowl_cls_assert_axiom.CowlClsAssertAxiom(exp.__Instance, ind.__Instance, null ); 
            instancesToRelease.Add(axiom.__Instance); 

            return cowl_ontology.CowlOntologyAddAxiom(targetOnto, axiom.__Instance); 
        }

        
        public static CowlClass CreateClassFromIri(string classIri)
        {
            var classObj = cowl_class.CowlClassFromString(UString.UstringCopyBuf(classIri));
            instancesToRelease.Add(classObj.__Instance); 
            return classObj;
        }

        
        public static CowlObjQuant CreateAllValuesRestriction(string propertyIri, IEnumerable<string> fillerClassesIri)
        {
            var fillerVec = cowl_vector.UvecCowlObjectPtr();
            foreach (var iri in fillerClassesIri)
            {
                var fillerCls = cowl_class.CowlClassFromString(UString.UstringCopyBuf(iri));
                instancesToRelease.Add(fillerCls.__Instance); 
                cowl_vector.UvecPushCowlObjectPtr(fillerVec, fillerCls.__Instance);
            }
            var opRole = cowl_vector.CowlVector(fillerVec); instancesToRelease.Add(opRole.__Instance); 
            var closure = cowl_nary_bool.CowlNaryBool(CowlNAryType.COWL_NT_INTERSECT, opRole); 
            instancesToRelease.Add(closure.__Instance); 
            var prop = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri)); 
            instancesToRelease.Add(prop.__Instance); 
            var quant = cowl_obj_quant.CowlObjQuant(CowlQuantType.COWL_QT_ALL, prop.__Instance, closure.__Instance); 
            instancesToRelease.Add(quant.__Instance); 
            return quant;
        }

       
        public static void SaveStaticOntology(string filePath)
        {
            if (Manager == null || Ontology == null) // Controlla che le istanze statiche siano inizializzate
            {
                Console.Error.WriteLine("ERRORE: Manager statico o Ontologia statica non inizializzati. Impossibile salvare.");
                return;
            }
            var pathUstr = UString.UstringCopyBuf(filePath);
            cowl_manager.CowlManagerWritePath(Manager, Ontology, pathUstr); 
        }

        public void Setup(Driver driver)
        {
            string absoluteHeaderDirectory = Path.GetFullPath("..\\..\\..\\include");

            var options = driver.Options;
            options.GeneratorKind = GeneratorKind.CSharp;
            options.OutputDir = Path.Combine(Environment.CurrentDirectory, "..\\..\\..\\CowlWrapper");

            var module = options.AddModule("cowl");
            module.IncludeDirs.Add(absoluteHeaderDirectory);
            module.IncludeDirs.Add(absoluteHeaderDirectory + "\\ulib");
            module.Headers.Add(absoluteHeaderDirectory + "\\cowl.h");

        }
        public void SetupPasses(Driver driver) {}
        public void Preprocess(Driver driver, ASTContext ctx) {}
        public void Postprocess(Driver driver, ASTContext ctx) {}

        
        public static void Main(string[] args)
        {
            // You must always initialize the library before use.
            cowl_config.CowlInit();

            // Instantiate a manager and deserialize an ontology from file.
            CowlManager manager = cowl_manager.CowlManager();

            var onto = cowl_manager.CowlManagerGetOntology(manager, cowl_ontology_id.CowlOntologyIdAnonymous());

            var playerClass = CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/CrewMateAlive");

            var objQuant = CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", new[] { "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Electrical_Sabotage" });

            var resultCreateInd = CreateIndividual(onto, "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Player1", new[] { playerClass }, new[] { objQuant });
            //write to file
            string absoluteHeaderDirectory2 = "amongus2.owl";
            var string3 = UString.UstringCopyBuf(absoluteHeaderDirectory2);
            cowl_sym_table.CowlSymTableRegisterPrefixRaw(cowl_ontology.CowlOntologyGetSymTable(onto), UString.UstringCopyBuf(""), UString.UstringCopyBuf("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/"), false);
            cowl_manager.CowlManagerWritePath(manager, onto, string3);

            //alternative output -> write to string
            UVec_char chars = uvec_builtin.UvecChar();
            cowl_manager.CowlManagerWriteStrbuf(manager, onto, chars);
            var sbyteArray = new sbyte[uvec_builtin.UvecCountChar(chars)];
            uvec_builtin.UvecCopyToArrayChar(chars, sbyteArray);
            byte[] byteArray = Array.ConvertAll(sbyteArray, b => (byte)b);
            string result = System.Text.Encoding.UTF8.GetString(byteArray);
            Console.WriteLine(result);
            cowl_object.CowlRelease(manager.__Instance);
            cowl_object.CowlRelease(onto.__Instance);

            foreach (var instance in instancesToRelease)
            {
                cowl_object.CowlRelease(instance);
            }

            //ConsoleDriver.Run(new CowlWrapper());
        }
    }
}
