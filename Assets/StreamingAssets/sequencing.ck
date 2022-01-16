// number of slots in sequencer
16 => int cols;
4 => int rows;
// duration between each slot
500::ms => dur BEAT;
// update rate for playhead position
5::ms => dur POS_RATE;
// increment per step
POS_RATE/BEAT => float posInc;

// arrays for sample file and folder names
["C5.wav", "D5.wav", "E5.wav", "F5.wav", "G5.wav", "A5.wav", "B5.wav"] @=> string smp1[];
["C4.wav", "D4.wav", "E4.wav", "F4.wav", "G4.wav", "A4.wav", "B4.wav"] @=> string smp2[];
["C3.wav", "D3.wav", "E3.wav", "F3.wav", "G3.wav", "A3.wav", "B3.wav"] @=> string smp3[];
["C2.wav", "D2.wav", "E2.wav", "F2.wav", "G2.wav", "A2.wav", "B2.wav"] @=> string smp4[];
[smp1, smp2, smp3, smp4] @=> string tracks[][];
["/Samples/", "/Samples2/", "/Samples3/", "/Samples4/"] @=> string folders[];
["C4.wav", "E4.wav", "G4.wav", "B4.wav", "C5.wav", "D5.wav", "E5.wav", "G5.wav", "B5.wav", "C6.wav"] @=> string layerSmp[];

// global (outgoing) variables for Unity
global int currentSlot;
global float playheadPos;

// global (incoming) data from Unity
global int editWhich;
global int editRow;
global float editGain;
global int editPitch;
global int editFolder;
global int patches;
global Event editHappened;
global Event startLayer;
global Event editLayer;

// array of which sample (pitch) to play across the sequence
string seqSmp[rows][cols];
// array of what gain to apply across the sequence
float seqGain[rows][cols];

// set default pitch (C) and gain (0) for each slot
for( int i; i < rows; i++)
{
    for( int j; j < cols; j++ )
    {
        0 => seqGain[i][j];
        tracks[i][0] => seqSmp[i][j];
    }
}

// buffers and reverb for sequence slots
SndBuf bufs[rows][cols];
NRev reverb => dac;
0.1 => reverb.mix;

// load in sequencer buffers; connect to output
for( int i; i < rows; i++)
{
    for( int j; j < cols; j++ )
    {
        bufs[i][j] => reverb;
        me.dir() + folders[0] + seqSmp[i][j] => bufs[i][j].read;
        0 => bufs[i][j].gain;
    }
}

// buffers and reverb for background audio layer
SndBuf layerBufs[10];
NRev verb2;
0.15 => verb2.mix;
Gain g[10];
float layerG[10];

// set background layer gain (0) and samples; connect to output
for( int i; i < 10; i++)
{
    layerBufs[i] => verb2 => g[i] => dac;
    0.0 => g[i].gain;
    0.0 => layerG[i];
    1 => layerBufs[i].loop;
    me.dir() + "/Layer/" + layerSmp[i] => layerBufs[i].read;
    1 => layerBufs[i].gain;
}

spork ~ playheadPosUpdate();
spork ~ listenForEdit();
spork ~ layer();
spork ~ listenForLayerEdit();

// main sequencer loop
while( true )
{
    // play current slots
    for( int i; i < rows; i++)
    {
        play(i, currentSlot, seqGain[i][currentSlot]);
    }
    
    // sync current slot and playhead position
    currentSlot => playheadPos;
    BEAT => now;
    currentSlot++;

    if( currentSlot >= cols )
    {
        0 => currentSlot;
    }
}

// start playback for given sequencer slot
fun void play( int row, int which, float gain )
{
    0 => bufs[row][which].pos;
    gain => bufs[row][which].gain;
}

// update playhead position to be sent to Unity
fun void playheadPosUpdate()
{
    while( true )
    {
        // pad playback position from sequencer edge
        if (playheadPos >= (cols - (2 * posInc)))
        {
            cols - (2 * posInc) => playheadPos;
        }
        else
        {
            // increment
            posInc +=> playheadPos;
        }

        POS_RATE => now;
    }
}

// route incoming edits from Unity
fun void listenForEdit()
{
    while( true )
    {
        // once notified
        editHappened => now;
        editGain => seqGain[editRow][editWhich];
        tracks[editRow][editPitch] => seqSmp[editRow][editWhich];
        0 => bufs[editRow][editWhich].gain;
        me.dir() + folders[editFolder] + seqSmp[editRow][editWhich] => bufs[editRow][editWhich].read;
    }
}

// start background audio layer
fun void layer()
{
    while( true )
    {
        // once notified
        startLayer => now;
        patchesToVol(patches) => float targetVol;
        layerG[0] => float startingVol;
        
        // start buffers
        for (int i; i < 10; i++)
        {
            0 => layerBufs[i].pos;
        }
        
        listenForLayerEdit();
    }
}

// change background audio layer gain according to number of patches
fun void listenForLayerEdit()
{
    while( true )
    {
        // once notified
        editLayer => now;
        patchesToVol(patches) => float targetVol;
        layerG[0] => float startingVol;
        
        // fade between current and target gain over 10 increments
        1 => int i;
        for (i; i <= 10; i++)
        {   
            // for each note in layer
            for (int j; j < 10; j++)
            {
                layerG[j] => float currentVol;
                startingVol + ((i / 10.0) * (targetVol - startingVol)) => g[j].gain;
            }
            
            100::ms => now;
        }
        
        // store new gains in array once fade completed
        for (int i; i < 10; i++)
        {
            targetVol => layerG[i];
        }
    }
}

// scale number of patches to gain
fun float patchesToVol(int p)
{
    float vol;
    900.0 => float scalar;
    
    if (16 < p < 32)
    {
        700.0 => scalar;
    }
    else
    {
        400.0 => scalar;
    }
    
    if (p <= 8)
    {
        0.0 => vol;
    }
    else
    {
        (p-8) * Math.log(p-8) / scalar => vol;
    }
    return vol;
}

    