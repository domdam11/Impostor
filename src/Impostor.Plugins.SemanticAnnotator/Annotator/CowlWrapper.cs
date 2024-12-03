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
    public class CowlWrapper
    {

        public static void Main(string[] args)
        {
            // Call the GetAnnotation method
            new CowlWrapper().GetAnnotation();
        }

        /// <summary>
        /// Retrieves the annotation.
        /// </summary>
        /// <returns>The annotation.</returns>
        public string GetAnnotation()
        {
            try
            {
                // Initialize the Cowl library
                cowl_config.CowlInit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            var instancesToRelease = new List<nint>();

            // Instantiate a manager and deserialize an ontology from file
            CowlManager manager = cowl_manager.CowlManager();
            var onto = cowl_manager.CowlManagerGetOntology(manager, cowl_ontology_id.CowlOntologyIdAnonymous());

            // Create a class from an IRI
            var playerClass = CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/CrewMateAlive", instancesToRelease);

            // Create an all values restriction
            var objQuant = CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", new[] { "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Electrical_Sabotage", "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Admin" }, instancesToRelease);

            // Create an individual
            var resultCreateInd = CreateIndividual(onto, "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Player1", new[] { playerClass }, new[] { objQuant }, instancesToRelease);

            // Write the ontology to a file
            string absoluteHeaderDirectory2 = "amongus2.owl";
            var string3 = UString.UstringCopyBuf(absoluteHeaderDirectory2);
            cowl_sym_table.CowlSymTableRegisterPrefixRaw(cowl_ontology.CowlOntologyGetSymTable(onto), UString.UstringCopyBuf(""), UString.UstringCopyBuf("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/"), false);
            cowl_manager.CowlManagerWritePath(manager, onto, string3);

            // Write the ontology to a string
            UVec_char chars = uvec_builtin.UvecChar();
            cowl_manager.CowlManagerWriteStrbuf(manager, onto, chars);
            var sbyteArray = new sbyte[uvec_builtin.UvecCountChar(chars)];
            uvec_builtin.UvecCopyToArrayChar(chars, sbyteArray);
            byte[] byteArray = Array.ConvertAll(sbyteArray, b => (byte)b);
            string result = System.Text.Encoding.UTF8.GetString(byteArray);

            // Release the instances
            foreach (var instance in instancesToRelease)
            {
                cowl_object.CowlRelease(instance);
            }
            cowl_object.CowlRelease(onto.__Instance);
            cowl_object.CowlRelease(manager.__Instance);

            return "prova" + DateTime.Now;
        }

        /// <summary>
        /// Creates an individual in the ontology.
        /// </summary>
        /// <param name="onto">The ontology.</param>
        /// <param name="individualIri">The IRI of the individual.</param>
        /// <param name="classesIri">The IRIs of the classes.</param>
        /// <param name="objQuantsIri">The IRIs of the object quantifiers.</param>
        /// <param name="instancesToRelease">The list of instances to release.</param>
        /// <returns>The CowlRet value.</returns>
        public static CowlRet CreateIndividual(CowlOntology onto, string individualIri, IEnumerable<CowlClass> classesIri, IEnumerable<CowlObjQuant> objQuantsIri, List<nint> instancesToRelease)
        {
            var operands = cowl_vector.UvecCowlObjectPtr();

            // Add the classes to the operands
            foreach (var classIri in classesIri)
            {
                cowl_vector.UvecPushCowlObjectPtr(operands, classIri.__Instance);
            }

            // Add the object quantifiers to the operands
            foreach (var objQuant in objQuantsIri)
            {
                cowl_vector.UvecPushCowlObjectPtr(operands, objQuant.__Instance);
            }

            CowlVector vec = cowl_vector.CowlVector(operands);
            instancesToRelease.Add(vec.__Instance);

            // Create the expression
            var exp = cowl_nary_bool.CowlNaryBool(CowlNAryType.COWL_NT_INTERSECT, vec);
            instancesToRelease.Add(exp.__Instance);

            // Create the individual
            var ind = cowl_named_ind.CowlNamedIndFromString(UString.UstringCopyBuf(individualIri));
            instancesToRelease.Add(ind.__Instance);

            // Create the axiom
            var axiom = cowl_cls_assert_axiom.CowlClsAssertAxiom(exp.__Instance, ind.__Instance, null);
            instancesToRelease.Add(axiom.__Instance);

            return cowl_ontology.CowlOntologyAddAxiom(onto, axiom.__Instance);
        }

        /// <summary>
        /// Creates a class from an IRI.
        /// </summary>
        /// <param name="classIri">The IRI of the class.</param>
        /// <param name="instancesToRelease">The list of instances to release.</param>
        /// <returns>The CowlClass object.</returns>
        public static CowlClass CreateClassFromIri(string classIri, List<nint> instancesToRelease)
        {
            var classObj = cowl_class.CowlClassFromString(UString.UstringCopyBuf(classIri));
            instancesToRelease.Add(classObj.__Instance);
            return classObj;
        }

        /// <summary>
        /// Creates an all values restriction.
        /// </summary>
        /// <param name="propertyIri">The IRI of the property.</param>
        /// <param name="fillerClassesIri">The IRIs of the filler classes.</param>
        /// <param name="instancesToRelease">The list of instances to release.</param>
        /// <returns>The CowlObjQuant object.</returns>
        public static CowlObjQuant CreateAllValuesRestriction(string propertyIri, IEnumerable<string> fillerClassesIri, List<nint> instancesToRelease)
        {
            var fillerVector = cowl_vector.UvecCowlObjectPtr();

            // Add the filler classes to the vector
            foreach (var fillerClassIri in fillerClassesIri)
            {
                var fillerClass = cowl_class.CowlClassFromString(UString.UstringCopyBuf(fillerClassIri));
                instancesToRelease.Add(fillerClass.__Instance);
                cowl_vector.UvecPushCowlObjectPtr(fillerVector, fillerClass.__Instance);
            }

            var operandsRole = cowl_vector.CowlVector(fillerVector);

            // Check the number of operands
            if (cowl_vector.CowlVectorCount(operandsRole) > 1)
            {
                // Create the closure
                var closure = cowl_nary_bool.CowlNaryBool(CowlNAryType.COWL_NT_INTERSECT, operandsRole);
                instancesToRelease.Add(closure.__Instance);

                // Create the task role
                var taskRole = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
                instancesToRelease.Add(taskRole.__Instance);

                // Create the object quantifier
                var obj_quant = cowl_obj_quant.CowlObjQuant(CowlQuantType.COWL_QT_ALL, taskRole.__Instance, closure.__Instance);
                instancesToRelease.Add(obj_quant.__Instance);
                instancesToRelease.Add(operandsRole.__Instance);

                return obj_quant;
            }
            else
            {
                // Create the task role
                var taskRole = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
                instancesToRelease.Add(taskRole.__Instance);

                // Create the object quantifier
                var obj_quant = cowl_obj_quant.CowlObjQuant(CowlQuantType.COWL_QT_ALL, taskRole.__Instance, operandsRole.__Instance);
                instancesToRelease.Add(obj_quant.__Instance);
                instancesToRelease.Add(operandsRole.__Instance);

                return obj_quant;
            }
        }

    }
}
