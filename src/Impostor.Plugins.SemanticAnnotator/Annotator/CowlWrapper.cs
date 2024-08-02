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
            var stringClass = UString.UstringWrapBuf("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Player");
            var cls = cowl_class.CowlClassFromString(stringClass);
            var axiom = cowl_decl_axiom.CowlDeclAxiom(cls.__Instance, null);
            cowl_ontology.CowlOntologyAddAxiom(onto, axiom.__Instance);
            
            /*string absoluteHeaderDirectory = "amongus.owl";
            
            var string1 = UString.UstringWrapBuf(absoluteHeaderDirectory);
            CowlOntology onto = cowl_manager.CowlManagerReadPath(manager, string1);
            var count = cowl_ontology.CowlOntologyAxiomCount(onto, false);
  
            string playerIri = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Player";
            var string2 = UString.UstringWrapBuf(playerIri);
            CowlClass cls = cowl_class.CowlClassFromString(string2);
            
            */

            string absoluteHeaderDirectory2 = "amongus2.owl";
            var string3 = UString.UstringWrapBuf(absoluteHeaderDirectory2);
            cowl_manager.CowlManagerWritePath(manager, onto, string3);
            cowl_object.CowlRelease(manager.__Instance);
            cowl_object.CowlRelease(onto.__Instance);
            cowl_object.CowlRelease(cls.__Instance);
            cowl_object.CowlRelease(axiom.__Instance);
            //ConsoleDriver.Run(new CowlWrapper());
        }
        public void Example()
        {
            
        }
    }
}
