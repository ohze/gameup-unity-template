#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <GoogleMobileAds/GoogleMobileAds.h>

extern UIViewController* UnityGetGLViewController();

typedef void (*Action_Void)();
typedef void (*Action_String)(const char* error);
typedef void (*Action_Double)(double value);

typedef NS_ENUM(NSInteger, AdState) {
    AdStateIdle, AdStateLoading, AdStateLoaded, AdStateShowing
};

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
    
    // Đưa AdChoices sang góc Trái trên cùng
    GADNativeAdViewAdOptions *viewOptions = [[GADNativeAdViewAdOptions alloc] init];
    viewOptions.preferredAdChoicesPosition = GADAdChoicesPositionTopLeftCorner;

    self.adLoader = [[GADAdLoader alloc] initWithAdUnitID:adUnitId
                                       rootViewController:rootVC
                                                  adTypes:@[GADAdLoaderAdTypeNative]
                                                  options:@[viewOptions]];
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
    
    // Tính toán lại kích thước để chứa thêm Description
    CGFloat headerHeight = 36.0;
    CGFloat mediaHeight = 180.0;
    CGFloat footerHeight = 68.0; // Tăng thêm 8dp để nhìn thoáng hơn
    CGFloat totalAdHeight = headerHeight + mediaHeight + footerHeight;
    CGFloat yPos = isTop ? safeArea.top : (rootView.bounds.size.height - safeArea.bottom - totalAdHeight);

    // Nền gốc màu tối
    self.currentAdLayout = [[UIView alloc] initWithFrame:CGRectMake(0, yPos, screenWidth, totalAdHeight)];
    self.currentAdLayout.backgroundColor = [UIColor colorWithRed:26.0/255.0 green:26.0/255.0 blue:26.0/255.0 alpha:1.0];
    
    GADNativeAdView *adView = [[GADNativeAdView alloc] initWithFrame:CGRectMake(0, headerHeight, screenWidth, mediaHeight + footerHeight)];
    [self.currentAdLayout addSubview:adView];
    
    // Ảnh / Video lớn
    GADMediaView *mediaView = [[GADMediaView alloc] initWithFrame:CGRectMake(0, 0, screenWidth, mediaHeight)];
    [adView addSubview:mediaView];
    adView.mediaView = mediaView;
    
    // App Icon (48x48)
    UIImageView *iconView = [[UIImageView alloc] initWithFrame:CGRectMake(10, mediaHeight + 10, 48, 48)];
    iconView.image = self.currentNativeAd.icon.image;
    iconView.contentMode = UIViewContentModeScaleAspectFill;
    iconView.clipsToBounds = YES;
    iconView.layer.cornerRadius = 8.0;
    [adView addSubview:iconView];
    adView.iconView = iconView;
    
    // Nút Call To Action nổi bật
    UIButton *ctaBtn = [UIButton buttonWithType:UIButtonTypeSystem];
    ctaBtn.frame = CGRectMake(screenWidth - 10 - 80, mediaHeight + 14, 80, 40);
    ctaBtn.backgroundColor = [UIColor colorWithRed:33.0/255.0 green:150.0/255.0 blue:243.0/255.0 alpha:1.0]; // Xanh dương
    [ctaBtn setTitleColor:[UIColor whiteColor] forState:UIControlStateNormal];
    ctaBtn.titleLabel.font = [UIFont boldSystemFontOfSize:13];
    [ctaBtn setTitle:self.currentNativeAd.callToAction forState:UIControlStateNormal];
    ctaBtn.layer.cornerRadius = 6.0;
    [adView addSubview:ctaBtn];
    adView.callToActionView = ctaBtn;
    
    // Headline (Tiêu đề)
    CGFloat textWidth = screenWidth - 10 - 48 - 10 - 80 - 10; 
    UILabel *headline = [[UILabel alloc] initWithFrame:CGRectMake(68, mediaHeight + 10, textWidth, 20)];
    headline.textColor = [UIColor whiteColor];
    headline.font = [UIFont boldSystemFontOfSize:15];
    headline.text = self.currentNativeAd.headline;
    [adView addSubview:headline];
    adView.headlineView = headline;
    
    // Body (Mô tả chi tiết)
    UILabel *body = [[UILabel alloc] initWithFrame:CGRectMake(68, mediaHeight + 32, textWidth, 18)];
    body.textColor = [UIColor colorWithRed:179.0/255.0 green:179.0/255.0 blue:179.0/255.0 alpha:1.0]; // Xám nhạt
    body.font = [UIFont systemFontOfSize:12];
    body.text = self.currentNativeAd.body;
    [adView addSubview:body];
    adView.bodyView = body;
    
    // Đăng ký AdView với Google
    adView.nativeAd = self.currentNativeAd;
    self.currentNativeAd.delegate = self;
    
    // Nút Hide (Collapse) ở Header
    UIButton *closeBtn = [UIButton buttonWithType:UIButtonTypeCustom];
    closeBtn.frame = CGRectMake(screenWidth - 64, 0, 64, headerHeight);
    [closeBtn setTitle:@"▼" forState:UIControlStateNormal];
    [closeBtn setTitleColor:[UIColor colorWithRed:224.0/255.0 green:224.0/255.0 blue:224.0/255.0 alpha:1.0] forState:UIControlStateNormal]; // Trắng xám sang trọng
    closeBtn.titleLabel.font = [UIFont boldSystemFontOfSize:16];
    [closeBtn addTarget:self action:@selector(closeTapped) forControlEvents:UIControlEventTouchUpInside];
    closeBtn.backgroundColor = [UIColor colorWithRed:18.0/255.0 green:18.0/255.0 blue:18.0/255.0 alpha:1.0];
    
    // Bo góc dưới trái nút Collapse
    UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:closeBtn.bounds byRoundingCorners:UIRectCornerBottomLeft cornerRadii:CGSizeMake(8.0, 8.0)];
    CAShapeLayer *maskLayer = [CAShapeLayer layer];
    maskLayer.path = maskPath.CGPath;
    closeBtn.layer.mask = maskLayer;
    [self.currentAdLayout addSubview:closeBtn];
    
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
    if (self.onClicked) self.onClicked();
}
@end

extern "C" {
    void NativeBanner_SetCallbacks(Action_Void onLoaded, Action_String onFailed, Action_Void onDisplayed, Action_Void onClosed, Action_Void onClicked, Action_Double onPaid) {
        NativeBannerManager *mgr = [NativeBannerManager sharedInstance];
        mgr.onLoaded = onLoaded; mgr.onFailed = onFailed; mgr.onDisplayed = onDisplayed;
        mgr.onClosed = onClosed; mgr.onClicked = onClicked; mgr.onPaid = onPaid;
    }
    void NativeBanner_LoadAd(const char* adUnitId) {
        [[NativeBannerManager sharedInstance] loadAd:[NSString stringWithUTF8String:adUnitId]];
    }
    void NativeBanner_ShowAd(bool isTop) {
        [[NativeBannerManager sharedInstance] showAd:isTop];
    }
    void NativeBanner_HideAd() {
        [[NativeBannerManager sharedInstance] hideAd];
    }
}