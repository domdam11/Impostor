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

namespace Impostor.Plugins.SemanticAnnotator.Utils
{
    public class CowlWrapper : ILibrary
    {
        public void Setup(Driver driver)
        {
            string absoluteHeaderDirectory = Path.GetFullPath("..\\..\\..\\include");
         
            var options = driver.Options;
            options.GeneratorKind = GeneratorKind.CSharp;
            options.OutputDir = Path.Combine(Environment.CurrentDirectory, "..\\..\\..\\CowlWrapper");

            var module = options.AddModule("cowl");
            module.IncludeDirs.Add(absoluteHeaderDirectory);
            module.IncludeDirs.Add(absoluteHeaderDirectory+"\\ulib");
            module.Headers.Add(absoluteHeaderDirectory + "\\cowl.h");

        }

        public void SetupPasses(Driver driver) { }

        public void Preprocess(Driver driver, ASTContext ctx) { }

        public void Postprocess(Driver driver, ASTContext ctx) { }

        public static void Main(string[] args)
        {
            // You must always initialize the library before use.
            cowl_config.CowlInit();

            // Instantiate a manager and deserialize an ontology from file.
            CowlManager manager = cowl_manager.CowlManager();
            
            var onto = cowl_manager.CowlManagerGetOntology(manager, cowl_ontology_id.CowlOntologyIdAnonymous());

            var playerClass = CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/CrewMateAlive");
           
            var objQuant =  CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", new[] { "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Electrical_Sabotage" });

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
        public static List<nint> instancesToRelease = new List<nint>();
        public static CowlRet CreateIndividual(CowlOntology onto, string individualIri, IEnumerable<CowlClass> classesIri, IEnumerable<CowlObjQuant> objQuantsIri)
        {
            var operands = cowl_vector.UvecCowlObjectPtr();
            foreach (var classIri in classesIri)
            {
                cowl_vector.UvecPushCowlObjectPtr(operands, classIri.__Instance);
            }
            foreach (var objQuant in objQuantsIri)
            {
                cowl_vector.UvecPushCowlObjectPtr(operands, objQuant.__Instance);
            }

            CowlVector vec = cowl_vector.CowlVector(operands);
            var exp = cowl_nary_bool.CowlNaryBool(CowlNAryType.COWL_NT_INTERSECT, vec);
            instancesToRelease.Add(exp.__Instance);
            var ind = cowl_named_ind.CowlNamedIndFromString(UString.UstringCopyBuf(individualIri));
            instancesToRelease.Add(ind.__Instance);
            var axiom = cowl_cls_assert_axiom.CowlClsAssertAxiom(exp.__Instance, ind.__Instance, null);
            instancesToRelease.Add(axiom.__Instance);
            return cowl_ontology.CowlOntologyAddAxiom(onto, axiom.__Instance);
        }

        public static CowlClass CreateClassFromIri(string classIri)
        {
            var classObj = cowl_class.CowlClassFromString(UString.UstringCopyBuf(classIri));
            instancesToRelease.Add(classObj.__Instance);
            return classObj;
        }

        public static CowlObjQuant CreateAllValuesRestriction(string propertyIri, IEnumerable<string> fillerClassesIri)
        {
            var fillerVector = cowl_vector.UvecCowlObjectPtr();

            foreach (var fillerClassIri in fillerClassesIri)
            {
                var fillerClass = cowl_class.CowlClassFromString(UString.UstringCopyBuf(fillerClassIri));
                cowl_vector.UvecPushCowlObjectPtr(fillerVector, fillerClass.__Instance);
            }
   
            var operandsRole = cowl_vector.CowlVector(fillerVector);
            instancesToRelease.Add(operandsRole.__Instance);
            var closure = cowl_nary_bool.CowlNaryBool(CowlNAryType.COWL_NT_INTERSECT, operandsRole);
            instancesToRelease.Add(closure.__Instance);
            var taskRole = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
            instancesToRelease.Add(taskRole.__Instance);
            var obj_quant = cowl_obj_quant.CowlObjQuant(CowlQuantType.COWL_QT_ALL, taskRole.__Instance, closure.__Instance);
            instancesToRelease.Add(obj_quant.__Instance);
            return obj_quant;
        }
        public void Example()
        {
            
        }
    }
}
