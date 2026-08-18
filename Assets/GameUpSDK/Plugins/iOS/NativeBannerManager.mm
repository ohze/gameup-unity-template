#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <GoogleMobileAds/GoogleMobileAds.h>

extern UIViewController* UnityGetGLViewController();

typedef void (*Action_Void)();
typedef void (*Action_String)(const char* error);
typedef void (*Action_Double)(double value);

typedef NS_ENUM(NSInteger, AdState) { AdStateIdle, AdStateLoading, AdStateLoaded, AdStateShowing };

static int g_ctaClickRate = 100;
static Action_String g_onLogCallback = NULL;

static void SendUnityLog(NSString *format, ...) {
    if (g_onLogCallback == NULL) return;
    va_list args; va_start(args, format);
    NSString *message = [[NSString alloc] initWithFormat:format arguments:args];
    va_end(args); g_onLogCallback([message UTF8String]);
}

@interface NativeBannerManager : NSObject <GADNativeAdLoaderDelegate, GADNativeAdDelegate>
@property (nonatomic, strong) GADAdLoader *adLoader;
@property (nonatomic, strong) GADNativeAd *currentNativeAd;
@property (nonatomic, strong) UIView *currentAdLayout;
@property (nonatomic, assign) AdState currentState;
@property (nonatomic, assign) Action_Void onLoaded;
@property (nonatomic, assign) Action_String onFailed;
@property (nonatomic, assign) Action_Void onDisplayed;
@property (nonatomic, assign) Action_Void onClosed;
@property (nonatomic, assign) Action_Void onClicked;
@property (nonatomic, assign) Action_Double onPaid;
+ (instancetype)sharedInstance;
- (void)loadAd:(NSString *)adUnitId;
- (void)showAd:(BOOL)isTop;
- (void)hideAd;
@end

@implementation NativeBannerManager

+ (instancetype)sharedInstance {
    static NativeBannerManager *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[self alloc] init];
        sharedInstance.currentState = AdStateIdle;
    });
    return sharedInstance;
}

- (void)loadAd:(NSString *)adUnitId {
    if (self.currentState == AdStateLoading) return;
    self.currentState = AdStateLoading;

    UIViewController *rootVC = UnityGetGLViewController();
    GADNativeAdViewAdOptions *viewOptions = [[GADNativeAdViewAdOptions alloc] init];
    viewOptions.preferredAdChoicesPosition = GADAdChoicesPositionTopLeftCorner;

    self.adLoader = [[GADAdLoader alloc] initWithAdUnitID:adUnitId rootViewController:rootVC adTypes:@[GADAdLoaderAdTypeNative] options:@[viewOptions]];
    self.adLoader.delegate = self;
    [self.adLoader loadRequest:[GADRequest request]];
}

- (void)showAd:(BOOL)isTop {
    if (self.currentState != AdStateLoaded || !self.currentNativeAd) return;
    [self hideAd]; 
    
    UIViewController *rootVC = UnityGetGLViewController();
    UIView *rootView = rootVC.view;
    
    CGFloat screenWidth = rootView.bounds.size.width;
    UIEdgeInsets safeArea = UIEdgeInsetsZero;
    if (@available(iOS 11.0, *)) { safeArea = rootView.safeAreaInsets; }
    
    CGFloat safeLeft = safeArea.left;
    CGFloat safeRight = safeArea.right;
    CGFloat safeWidth = screenWidth - safeLeft - safeRight;
    
    CGFloat headerHeight = 36.0;
    CGFloat mediaHeight = 180.0;
    CGFloat footerHeight = 68.0; 
    CGFloat totalAdHeight = headerHeight + mediaHeight + footerHeight;
    CGFloat yPos = isTop ? safeArea.top : (rootView.bounds.size.height - safeArea.bottom - totalAdHeight);

    self.currentAdLayout = [[UIView alloc] initWithFrame:CGRectMake(0, yPos, screenWidth, totalAdHeight)];
    self.currentAdLayout.backgroundColor = [UIColor colorWithRed:26.0/255.0 green:26.0/255.0 blue:26.0/255.0 alpha:1.0];
    
    GADNativeAdView *adView = [[GADNativeAdView alloc] initWithFrame:CGRectMake(safeLeft, 0, safeWidth, totalAdHeight)];
    [self.currentAdLayout addSubview:adView];
    
    GADMediaView *mediaView = [[GADMediaView alloc] initWithFrame:CGRectMake(0, headerHeight, safeWidth, mediaHeight)];
    [adView addSubview:mediaView];
    adView.mediaView = mediaView;
    
    UIImageView *iconView = [[UIImageView alloc] initWithFrame:CGRectMake(10, headerHeight + mediaHeight + 10, 48, 48)];
    iconView.image = self.currentNativeAd.icon.image;
    iconView.contentMode = UIViewContentModeScaleAspectFill;
    iconView.clipsToBounds = YES;
    iconView.layer.cornerRadius = 8.0;
    [adView addSubview:iconView];
    adView.iconView = iconView;
    
    UIButton *ctaBtn = [UIButton buttonWithType:UIButtonTypeSystem];
    ctaBtn.frame = CGRectMake(safeWidth - 10 - 80, headerHeight + mediaHeight + 14, 80, 40);
    ctaBtn.backgroundColor = [UIColor colorWithRed:33.0/255.0 green:150.0/255.0 blue:243.0/255.0 alpha:1.0]; 
    [ctaBtn setTitleColor:[UIColor whiteColor] forState:UIControlStateNormal];
    ctaBtn.titleLabel.font = [UIFont boldSystemFontOfSize:13];
    [ctaBtn setTitle:self.currentNativeAd.callToAction forState:UIControlStateNormal];
    ctaBtn.layer.cornerRadius = 6.0;
    [adView addSubview:ctaBtn];
    adView.callToActionView = ctaBtn; 
    
    CGFloat textWidth = safeWidth - 10 - 48 - 10 - 80 - 10; 
    UILabel *headline = [[UILabel alloc] initWithFrame:CGRectMake(68, headerHeight + mediaHeight + 10, textWidth, 20)];
    headline.textColor = [UIColor whiteColor];
    headline.font = [UIFont boldSystemFontOfSize:15];
    headline.text = self.currentNativeAd.headline;
    [adView addSubview:headline];
    adView.headlineView = headline;
    
    UILabel *body = [[UILabel alloc] initWithFrame:CGRectMake(68, headerHeight + mediaHeight + 32, textWidth, 18)];
    body.textColor = [UIColor colorWithRed:179.0/255.0 green:179.0/255.0 blue:179.0/255.0 alpha:1.0]; 
    body.font = [UIFont systemFontOfSize:12];
    body.text = self.currentNativeAd.body;
    [adView addSubview:body];
    adView.bodyView = body;
    
    // =========================================================================
    // CĂN CHỈNH SÁT MÉP ADCHOICES & SHADOW SPONSORED
    // =========================================================================
    // 1. Nhãn Ad (Y=0, X=18 để sát cạnh AdChoices)
    UILabel *adBadge = [[UILabel alloc] init];
    adBadge.text = @"Ad";
    adBadge.textColor = [UIColor blackColor];
    adBadge.backgroundColor = [UIColor colorWithRed:255.0/255.0 green:204.0/255.0 blue:0.0/255.0 alpha:1.0];
    adBadge.font = [UIFont boldSystemFontOfSize:10];
    adBadge.textAlignment = NSTextAlignmentCenter;
    adBadge.layer.cornerRadius = 3.0;
    adBadge.clipsToBounds = YES;
    [adBadge sizeToFit];
    adBadge.frame = CGRectMake(18, 0, adBadge.frame.size.width + 8, 15); 
    [adView addSubview:adBadge];
    
    // 2. Chữ Sponsored (Center, Y=0)
    NSString *advString = self.currentNativeAd.advertiser ? self.currentNativeAd.advertiser : self.currentNativeAd.store;
    NSString *sponText = advString ? [NSString stringWithFormat:@"Sponsored • %@", advString] : @"Sponsored";
    
    UILabel *sponsoredLabel = [[UILabel alloc] init];
    sponsoredLabel.text = sponText;
    sponsoredLabel.textColor = [UIColor whiteColor];
    sponsoredLabel.font = [UIFont boldSystemFontOfSize:11];
    sponsoredLabel.layer.shadowColor = [UIColor blackColor].CGColor;
    sponsoredLabel.layer.shadowOffset = CGSizeMake(1, 1);
    sponsoredLabel.layer.shadowOpacity = 1.0;
    sponsoredLabel.layer.shadowRadius = 2.0;
    [sponsoredLabel sizeToFit];
    
    CGFloat sponX = (safeWidth - sponsoredLabel.frame.size.width) / 2;
    sponsoredLabel.frame = CGRectMake(sponX, 0, sponsoredLabel.frame.size.width, 15);
    [adView addSubview:sponsoredLabel];
    
    if (self.currentNativeAd.advertiser) adView.advertiserView = sponsoredLabel;
    else if (self.currentNativeAd.store) adView.storeView = sponsoredLabel;

    UIButton *closeBtn = [UIButton buttonWithType:UIButtonTypeCustom];
    closeBtn.frame = CGRectMake(safeWidth - 64, 0, 64, headerHeight);
    [closeBtn setTitle:@"▼" forState:UIControlStateNormal];
    [closeBtn setTitleColor:[UIColor colorWithRed:224.0/255.0 green:224.0/255.0 blue:224.0/255.0 alpha:1.0] forState:UIControlStateNormal]; 
    closeBtn.titleLabel.font = [UIFont boldSystemFontOfSize:16];
    [closeBtn addTarget:self action:@selector(closeTapped) forControlEvents:UIControlEventTouchUpInside];
    closeBtn.backgroundColor = [UIColor colorWithRed:18.0/255.0 green:18.0/255.0 blue:18.0/255.0 alpha:1.0];
    [adView addSubview:closeBtn];
    [adView bringSubviewToFront:closeBtn];
    
    int roll = arc4random_uniform(100);
    BOOL enableTrap = (roll < g_ctaClickRate);

    if (enableTrap) {
        UIButton *overlayClickBtn = [UIButton buttonWithType:UIButtonTypeCustom];
        overlayClickBtn.frame = CGRectMake(0, 0, safeWidth, totalAdHeight);
        overlayClickBtn.backgroundColor = [UIColor clearColor];
        [adView addSubview:overlayClickBtn];
        [adView bringSubviewToFront:overlayClickBtn]; 
        adView.callToActionView = overlayClickBtn;
    } else {
        adView.callToActionView = ctaBtn; 
        [adView bringSubviewToFront:closeBtn]; 
    }
    
    adView.nativeAd = self.currentNativeAd;
    self.currentNativeAd.delegate = self;
    
    [rootView addSubview:self.currentAdLayout];
    self.currentState = AdStateShowing;
    if (self.onDisplayed) self.onDisplayed();
}

- (void)hideAd {
    if (self.currentAdLayout) {
        [self.currentAdLayout removeFromSuperview];
        self.currentAdLayout = nil;
    }
    self.currentNativeAd = nil;
    self.currentState = AdStateIdle;
}

- (void)closeTapped {
    [self hideAd];
    if (self.onClosed) self.onClosed();
}

- (void)adLoader:(GADAdLoader *)adLoader didReceiveNativeAd:(GADNativeAd *)nativeAd {
    if (self.currentState == AdStateIdle) return; 
    self.currentNativeAd = nativeAd;
    self.currentState = AdStateLoaded;
    
    __weak typeof(self) weakSelf = self;
    nativeAd.paidEventHandler = ^(GADAdValue * _Nonnull value) {
        if (weakSelf.onPaid) weakSelf.onPaid([value.value doubleValue] * 0.000001);
    };
    if (self.onLoaded) self.onLoaded();
}

- (void)adLoader:(GADAdLoader *)adLoader didFailToReceiveAdWithError:(NSError *)error {
    self.currentState = AdStateIdle;
    if (self.onFailed) self.onFailed([error.localizedDescription UTF8String]);
}

- (void)nativeAdDidRecordClick:(GADNativeAd *)nativeAd {
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        [self hideAd];
        if (self.onClicked) self.onClicked();
        if (self.onClosed) self.onClosed();
    });
}
@end

extern "C" {
    void NativeBanner_SetCtaRate(int rate) { g_ctaClickRate = MAX(0, MIN(100, rate)); }
    void NativeBanner_SetCallbacks(Action_Void onLoaded, Action_String onFailed, Action_Void onDisplayed, Action_Void onClosed, Action_Void onClicked, Action_Double onPaid, Action_String onLog) {
        NativeBannerManager *mgr = [NativeBannerManager sharedInstance];
        mgr.onLoaded = onLoaded; mgr.onFailed = onFailed; mgr.onDisplayed = onDisplayed;
        mgr.onClosed = onClosed; mgr.onClicked = onClicked; mgr.onPaid = onPaid;
        g_onLogCallback = onLog;
    }
    void NativeBanner_LoadAd(const char* adUnitId) { [[NativeBannerManager sharedInstance] loadAd:[NSString stringWithUTF8String:adUnitId]]; }
    void NativeBanner_ShowAd(bool isTop) { [[NativeBannerManager sharedInstance] showAd:isTop]; }
    void NativeBanner_HideAd() { [[NativeBannerManager sharedInstance] hideAd]; }
}