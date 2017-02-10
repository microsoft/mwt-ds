/*
 * decision-service 
 * 
 * client module to interact with the decision serivice. 
 * Currently it just genereates the DS script url from a valid 
 * context object. Can be used server or in the browser with browserfy.
 * 
 * author     (Jonathan Crockett<jonathanc@complex.com>)
 * 
 * @todo add ability to make requests from module and send rewards
 */

'use strict';

/**
 * Takes a context object and converts it to a DS script path.
 *
 * @param      {obj}    Valid context object
 * @return     {string} A DS formatted path
 */
exports.contextToDSPath = function(context) {

    var outputUrlSegments = [];
    var outputPath = '';

    for (var siteId in context) {

        outputUrlSegments.push(generateSiteFeatureUrlSegments(siteId, context[siteId]));

    }

    outputPath = outputUrlSegments.join('/');

    return outputPath;

}


exports.generateScriptUrl = function(options) {

    if (!options.hostname) {
        options.hostname = 'ds.microsoft.com';
    }

    if (!options.protocol) {
        options.protocol = 'http:';
    }

    if (!options.origin) {
        options.origin = options.protocol + '//' + options.hostname;
    }

    var url = options.origin + '/api/' + options.endpoint + '/' + options.jsonpCallbackName + '/' + exports.contextToDSPath(options.context);

    if (options.detail) {
        url += '?detail=1'; 
    }

    return url;

}


/**
 * Takes a site context object and returns
 * an array of segments
 *
 * @param 		{string} siteId
 * @param      {obj}  siteContextJson  The site context json
 * @return     {Array}   { description_of_the_return_value }
 */
function generateSiteFeatureUrlSegments(siteId, siteContext) {

    var outputUrlSegments = [];

    var arms = [];
    var sharedFeatures = [];
    var sharedFeaturesEscapeChar = '!';
    var sharedFeaturesDelimiter = ';';

    var siteUrlSegment = '~' + siteId;

    for (var prop in siteContext) {

        if (prop != '_multi') {

            if (prop[0] == '_') {
                sharedFeatures.push(siteContext[prop]);
            } else {
                sharedFeatures.push(prop + '~' + extractFeatures(siteContext[prop]));
            }

        } else {

            for (var x = 0; x < siteContext[prop].length; x++) {

                arms.push(extractFeatures(siteContext[prop][x]).join(';'));

            }

            outputUrlSegments.push(arms.join('/'));

        }

    }

    if (sharedFeatures.length > 0) {
        siteUrlSegment += sharedFeaturesEscapeChar + sharedFeatures.join(sharedFeaturesDelimiter);
    }

    outputUrlSegments.unshift(siteUrlSegment);

    return outputUrlSegments.join('/');
}


/**
 * converts features object to array recursively.
 *
 * @param      {obj}  featureObject Context object
 * @return     {Array}   Array of features and nested features.
 */
function extractFeatures(featureObject) {

    var output = '', features = [];

    for (var key in featureObject) {

        if (typeof key == 'string' && key[0] == '_') { // ID

            features.push(featureObject[key]);

        } else if (typeof featureObject[key] == 'object') {

            features.push(key + '~' + extractFeatures(featureObject[key]).join(','));

        } else if (typeof(featureObject[key]) == 'boolean' && featureObject[key]) { // Boolean

            features.push(key);

        } else if (!isNaN(featureObject[key])) { // Numeric

            features.push(key + '=' + featureObject[key]);

        }

    }

    return features;

}