using System.Text;
using cowl;

namespace CowlSharp.Wrapper
{
    public static class CowlWrapper
    {
        /// <summary>
        /// Creates an individual in the ontology.
        /// </summary>
        /// <param name="onto">The ontology.</param>
        /// <param name="individualIri">The IRI of the individual.</param>
        /// <param name="classesIri">The IRIs of the classes.</param>
        /// <param name="objQuantsIri">The IRIs of the object quantifiers.</param>
        /// <param name="instancesToRelease">The list of instances to release.</param>
        /// <returns>The CowlRet value.</returns>
        public static CowlRet CreateIndividual(CowlOntology onto, string individualIri, IEnumerable<CowlClass> classesIri, IEnumerable<CowlObjQuant> objQuantsIri, IEnumerable<CowlDataQuant>? dataQuantsIri, List<nint> instancesToRelease, Boolean isPlayer = true, IEnumerable<CowlObjCard>? cardrestriction = null)
        {
            var operands = cowl_vector.UvecCowlObjectPtr();

            // Add the classes to the operands
            foreach (var classIri in classesIri)
            {
                cowl_vector.UvecPushCowlObjectPtr(operands, classIri.__Instance);
            }

            if (isPlayer)
            {
                // Add the object quantifiers to the operands
                foreach (var objQuant in objQuantsIri)
                {
                    cowl_vector.UvecPushCowlObjectPtr(operands, objQuant.__Instance);
                }
            }
            
            if (cardrestriction != null)
            {
                foreach (var card in cardrestriction)
                {
                    cowl_vector.UvecPushCowlObjectPtr(operands, card.__Instance);  
                }  
            }
            
            if (dataQuantsIri != null)
            {
                foreach (var dataQuant in dataQuantsIri)
                {
                    cowl_vector.UvecPushCowlObjectPtr(operands, dataQuant.__Instance);
                }
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

        public static string GetFullIri(string baseIri, string fragment)
        {
            // Uso di StringBuilder
            var iriBuilder = new StringBuilder(baseIri);

            // Aggiunge "#" solo se necessario
            if (!baseIri.EndsWith("#") && !baseIri.EndsWith("/"))
            {
                iriBuilder.Append("/");
            }

            // Aggiunge il frammento
            iriBuilder.Append(fragment);
            return iriBuilder.ToString();
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
        /// Creates a min cardinality restriction not qualified (filler = THING)
        /// </summary>
        /// <param name="prop">property</param>
        /// <param name="cardinality">card of restriction</param>
        /// <param name="instancesToRelease">The list of instances to release.</param>
        /// <returns>The CowlObjCard object.</returns>
        public static CowlObjCard CreateCardTypeMin(CowlObjProp prop ,int cardinality, List<nint> instancesToRelease)
        {
            
            CowlClass filler = CreateClassFromIri("http://www.w3.org/2002/07/owl#Thing" ,instancesToRelease);
            var cardrestriction = cowl_obj_card.CowlObjCard(CowlCardType.COWL_CT_MIN, prop.__Instance, filler.__Instance, (uint)cardinality);
            instancesToRelease.Add(cardrestriction.__Instance);
            return cardrestriction;

        }
        /// <summary>
        /// Creates a max cardinality restriction not qualified (filler = THING)
        /// </summary>
        /// <param name="prop">property</param>
        /// <param name="cardinality">card of restriction</param>
        /// <param name="instancesToRelease">The list of instances to release.</param>
        /// <returns>The CowlClass object.</returns>
        public static CowlObjCard CreateCardTypeMax(CowlObjProp prop ,int cardinality, List<nint> instancesToRelease)
        {
            
            CowlClass filler = CreateClassFromIri("http://www.w3.org/2002/07/owl#Thing" ,instancesToRelease);
            var cardrestriction = cowl_obj_card.CowlObjCard(CowlCardType.COWL_CT_MAX, prop.__Instance, filler.__Instance, (uint)cardinality);
            instancesToRelease.Add(cardrestriction.__Instance);
            return cardrestriction;

        }
        
        /// <summary>
        /// Creates an exactly cardinality restriction not qualified (filler = THING, exactly = minX and maxX)
        /// </summary>
        /// <param name="prop">property</param>
        /// <param name="cardinality">card of restriction</param>
        /// <param name="instancesToRelease">The list of instances to release.</param>
        /// <returns>The CowlClass object.</returns>
        public static CowlObjCard CreateCardTypeExactly(CowlObjProp prop ,int cardinality, List<nint> instancesToRelease)
        {
            
            CowlClass filler = CreateClassFromIri("http://www.w3.org/2002/07/owl#Thing" ,instancesToRelease);
            var cardrestriction = cowl_obj_card.CowlObjCard(CowlCardType.COWL_CT_EXACT, prop.__Instance, filler.__Instance, (uint)cardinality);
            instancesToRelease.Add(cardrestriction.__Instance);
            return cardrestriction;

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
                var role = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
                instancesToRelease.Add(role.__Instance);

                // Create the object quantifier
                var obj_quant = cowl_obj_quant.CowlObjQuant(CowlQuantType.COWL_QT_ALL, role.__Instance, closure.__Instance);
                instancesToRelease.Add(obj_quant.__Instance);
                instancesToRelease.Add(operandsRole.__Instance);

                return obj_quant;
            }
            else
            {
                // Create the role
                var role = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
                instancesToRelease.Add(role.__Instance);

                // Create the object quantifier
                var obj_quant = cowl_obj_quant.CowlObjQuant(CowlQuantType.COWL_QT_ALL, role.__Instance, operandsRole.__Instance);
                instancesToRelease.Add(obj_quant.__Instance);
                instancesToRelease.Add(operandsRole.__Instance);

                return obj_quant;
            }
        }

        //e.g. :Reports
        public static CowlObjProp CreateObjPropFromIri(string propertyIri, List<nint> instancesToRelease)
        {
            var role = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
            instancesToRelease.Add(role.__Instance);

            return role;
        }


        //e.g :CrewmateName
        public static CowlDataProp CreateDataPropFromIri(string dataPropIri, List<nint> instancesToRelease)
        {
            var dataPropObj = cowl_data_prop.CowlDataPropFromString(UString.UstringCopyBuf(dataPropIri));
            instancesToRelease.Add(dataPropObj.__Instance);

            return dataPropObj;
        }

        //e.g ObjHasValue(:hasParent :John)
        public static CowlObjHasValue CreateObjHasValueRestriction(string propertyIri, string individualIri, List<nint> instancesToRelease)
        {
            var property = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
            instancesToRelease.Add(property.__Instance);

            var ind = cowl_named_ind.CowlNamedIndFromString(UString.UstringCopyBuf(individualIri));
            instancesToRelease.Add(ind.__Instance);

            var objRestr = cowl_obj_has_value.CowlObjHasValue(property.__Instance, ind.__Instance);
            instancesToRelease.Add(objRestr.__Instance);

            return objRestr;
        }


        public static CowlDataQuant CreateDataValuesRestriction(string propertyIri, IEnumerable<string> fillerLiterals, string dt, string lang, List<nint> instancesToRelease)
        {
            var fillerVector = cowl_vector.UvecCowlObjectPtr();

            // Add the filler literals to the vector
            foreach (var fillerLiteralIri in fillerLiterals)
            {
                var fillerLiteral = cowl_literal.CowlLiteralFromString(UString.UstringCopyBuf(dt), UString.UstringCopyBuf(fillerLiteralIri), UString.UstringCopyBuf(lang));
                instancesToRelease.Add(fillerLiteral.__Instance);
                cowl_vector.UvecPushCowlObjectPtr(fillerVector, fillerLiteral.__Instance);
            }

            var operandsRole = cowl_vector.CowlVector(fillerVector);

            // Check the number of operands
            if (cowl_vector.CowlVectorCount(operandsRole) > 1)
            {
                // Create the closure
                var closure = cowl_nary_bool.CowlNaryBool(CowlNAryType.COWL_NT_INTERSECT, operandsRole);
                instancesToRelease.Add(closure.__Instance);

                // Create the role
                var role = cowl_data_prop.CowlDataPropFromString(UString.UstringCopyBuf(propertyIri));
                instancesToRelease.Add(role.__Instance);

                //e.g. DataOneOf("something1", "something2")
                var range = cowl_data_one_of.CowlDataOneOf(operandsRole);
                instancesToRelease.Add(operandsRole.__Instance);

                // Create the object quantifier
                var data_quant = cowl_data_quant.CowlDataQuant(CowlQuantType.COWL_QT_ALL, role.__Instance, range.__Instance);
                instancesToRelease.Add(data_quant.__Instance);
                instancesToRelease.Add(range.__Instance);

                return data_quant;
            }
            else
            {
                // Create the role
                var role = cowl_data_prop.CowlDataPropFromString(UString.UstringCopyBuf(propertyIri));
                instancesToRelease.Add(role.__Instance);

                //e.g. DataOneOf("something1", "something2")
                var range = cowl_data_one_of.CowlDataOneOf(operandsRole);
                instancesToRelease.Add(operandsRole.__Instance);

                // Create the object quantifier
                var data_quant = cowl_data_quant.CowlDataQuant(CowlQuantType.COWL_QT_ALL, role.__Instance, range.__Instance);
                instancesToRelease.Add(data_quant.__Instance);
                instancesToRelease.Add(range.__Instance);

                return data_quant;
            }
        }


        

    }
}
