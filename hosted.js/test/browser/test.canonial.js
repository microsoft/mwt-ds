// import "mocha";
// import { assert } from "chai";
// import fixture from "karma-fixture";

describe("DecisionServiceBrowser", () => {
   beforeEach(function() {
       fixture.setBase('/base/test/browser');
    fixture.load('test.canonical.html'); 
});

    it("Simple actions", () => {
        // var request = {
        //     site: 'abc',
        //     actions: [
        //         { ids: [{ id: '1' }] },
        //         { ids: [{ id: '2' }] },
        //     ],
        //     actionSets: null,
        //     shared: null
        // };

        // assert.equal(DecisionServiceRequestUtil.requestToPath(<DecisionServiceRankRequest>request), '~abc/1/2');
    });


});